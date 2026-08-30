// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Stickers;

public sealed class StickerCollectionStore
{
    private readonly IStickerRepository _repository;
    private readonly StickerSetLookup _lookup;
    private readonly StickerAccountStore _accounts;
    private readonly StickerDocumentIndex _documents;
    private readonly StickerUpdateNotifier _notifier;
    private readonly TimeProvider _timeProvider;

    public StickerCollectionStore(IStickerRepository repository,
        StickerSetLookup lookup, StickerAccountStore accounts,
        StickerDocumentIndex documents, StickerUpdateNotifier notifier,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _lookup = lookup;
        _accounts = accounts;
        _documents = documents;
        _notifier = notifier;
        _timeProvider = timeProvider;
    }

    public async ValueTask<TLBytes> InstallAsync(long userId, long authKeyId,
        long? setId, long? accessHash, string? shortName, bool archived)
    {
        using TLStickerSetState? set = await _lookup.ResolveSetAsync(setId,
            accessHash, shortName);
        if (set is null)
        {
            return StickerResults.Error("STICKERSET_INVALID");
        }

        long id = set.Value.AsStickerSetState().SetId;
        StickerAccountSnapshot account = await _accounts.ReadAsync(userId);
        long[] displaced = [];
        if (archived)
        {
            account = account with
            {
                Installed = StickerAccountStore.Remove(account.Installed, id),
                Archived = StickerAccountStore.Prepend(account.Archived, id),
            };
        }
        else
        {
            long[] archivedIds = StickerAccountStore.Remove(account.Archived, id);
            displaced = account.Installed.Where(value => value != id)
                .Skip(StickerRows.CollectionLimit - 1).ToArray();
            foreach (long displacedId in displaced)
            {
                archivedIds = StickerAccountStore.Prepend(archivedIds, displacedId);
            }
            account = account with
            {
                Archived = archivedIds,
                Installed = StickerAccountStore.Prepend(account.Installed, id),
            };
        }
        if (!await _accounts.PersistAsync(userId, account))
        {
            return StickerResults.StorageError();
        }
        await _notifier.NotifySetsAsync(userId, authKeyId, StickerRows.Kind(
            set.Value.AsStickerSetState().Get_SetView().AsStickerSet()));
        if (displaced.Length > 0)
        {
            IReadOnlyCollection<TLStickerSetState> rows =
                await _repository.GetSetsAsync();
            try
            {
                var sets = new Vector();
                foreach (TLStickerSetState row in rows.Where(row =>
                             displaced.Contains(row.AsStickerSetState().SetId)))
                {
                    using var covered = StickerSetNoCovered.Builder()
                        .Set(row.AsStickerSetState().Get_SetView()
                            .AsStickerSet().ToReadOnlySpan()).Build();
                    sets.AppendTLObject(covered.ToReadOnlySpan());
                }
                var archivedResult = StickerSetInstallResultArchive.Builder()
                    .Sets(sets).Build();
                return archivedResult.TLBytes!.Value;
            }
            finally
            {
                StickerRows.Dispose(rows);
            }
        }
        var result = StickerSetInstallResultSuccess.Builder().Build();
        return result.TLBytes!.Value;
    }

    public async ValueTask<TLBytes> UninstallAsync(long userId, long authKeyId,
        long? setId, long? accessHash, string? shortName)
    {
        using TLStickerSetState? set = await _lookup.ResolveSetAsync(setId,
            accessHash, shortName);
        if (set is null)
        {
            return StickerResults.Error("STICKERSET_INVALID");
        }
        long id = set.Value.AsStickerSetState().SetId;
        StickerAccountSnapshot account = await _accounts.ReadAsync(userId);
        account = account with
        {
            Installed = StickerAccountStore.Remove(account.Installed, id),
            Archived = StickerAccountStore.Remove(account.Archived, id),
        };
        if (!await _accounts.PersistAsync(userId, account))
        {
            return StickerResults.StorageError();
        }
        await _notifier.NotifySetsAsync(userId, authKeyId, StickerRows.Kind(
            set.Value.AsStickerSetState().Get_SetView().AsStickerSet()));
        return StickerResults.True();
    }

    public async ValueTask<TLBytes> ToggleSetsAsync(long userId, long authKeyId,
        long[] setIds, bool uninstall, bool archive, bool unarchive)
    {
        if ((uninstall ? 1 : 0) + (archive ? 1 : 0) +
            (unarchive ? 1 : 0) != 1)
        {
            return StickerResults.Error("STICKERSET_INVALID");
        }
        var kinds = new HashSet<StickerSetKind>();
        foreach (long id in setIds)
        {
            using TLStickerSetState? row = await _repository.GetSetAsync(id);
            if (row is null)
            {
                return StickerResults.Error("STICKERSET_INVALID");
            }
            kinds.Add(StickerRows.Kind(row.Value.AsStickerSetState()
                .Get_SetView().AsStickerSet()));
        }

        StickerAccountSnapshot account = await _accounts.ReadAsync(userId);
        long[] installed = account.Installed;
        long[] archivedIds = account.Archived;
        foreach (long id in setIds)
        {
            if (uninstall)
            {
                installed = StickerAccountStore.Remove(installed, id);
                archivedIds = StickerAccountStore.Remove(archivedIds, id);
            }
            else if (archive)
            {
                installed = StickerAccountStore.Remove(installed, id);
                archivedIds = StickerAccountStore.Prepend(archivedIds, id);
            }
            else
            {
                archivedIds = StickerAccountStore.Remove(archivedIds, id);
                installed = StickerAccountStore.Prepend(installed, id);
            }
        }
        if (!await _accounts.PersistAsync(userId,
                account with { Installed = installed, Archived = archivedIds }))
        {
            return StickerResults.StorageError();
        }
        foreach (StickerSetKind kind in kinds)
        {
            await _notifier.NotifySetsAsync(userId, authKeyId, kind);
        }
        return StickerResults.True();
    }

    public async ValueTask<TLBytes> ReorderAsync(long userId, long authKeyId,
        StickerSetKind kind, long[] order)
    {
        if (order.Length != order.Distinct().Count())
        {
            return StickerResults.Error("STICKERSET_INVALID");
        }
        StickerAccountSnapshot account = await _accounts.ReadAsync(userId);
        long[] installed = account.Installed;

        var matching = new List<long>();
        var other = new List<long>();
        foreach (long id in installed)
        {
            using TLStickerSetState? row = await _repository.GetSetAsync(id);
            if (row is not null && StickerRows.Kind(row.Value
                    .AsStickerSetState().Get_SetView().AsStickerSet()) == kind)
            {
                matching.Add(id);
            }
            else
            {
                other.Add(id);
            }
        }
        if (!matching.ToHashSet().SetEquals(order))
        {
            return StickerResults.Error("STICKERSET_INVALID");
        }
        var reordered = new List<long>(installed.Length);
        int matchingIndex = 0;
        int otherIndex = 0;
        foreach (long id in installed)
        {
            using TLStickerSetState? row = await _repository.GetSetAsync(id);
            bool isKind = row is not null && StickerRows.Kind(row.Value
                .AsStickerSetState().Get_SetView().AsStickerSet()) == kind;
            reordered.Add(isKind ? order[matchingIndex++] : other[otherIndex++]);
        }
        if (!await _accounts.PersistAsync(userId,
                account with { Installed = reordered.ToArray() }))
        {
            return StickerResults.StorageError();
        }
        await _notifier.NotifySetsAsync(userId, authKeyId, kind);
        return StickerResults.True();
    }

    public async ValueTask<TLBytes> ReadFeaturedAsync(long userId,
        long authKeyId, long[] ids)
    {
        StickerAccountSnapshot account = await _accounts.ReadAsync(userId);
        long[] featuredRead = account.FeaturedRead;
        foreach (long id in ids)
        {
            featuredRead = StickerAccountStore.Prepend(featuredRead, id);
        }
        if (!await _accounts.PersistAsync(userId,
                account with { FeaturedRead = featuredRead }))
        {
            return StickerResults.StorageError();
        }
        await _notifier.NotifyFeaturedReadAsync(userId, authKeyId);
        return StickerResults.True();
    }

    public async ValueTask<TLBytes> SaveCollectionDocumentAsync(long userId,
        long authKeyId, long id, long accessHash, StickerCollection collection,
        bool remove)
    {
        StickerSetKind? requiredKind = collection switch
        {
            StickerCollection.Recent => StickerSetKind.Regular,
            StickerCollection.AttachedRecent => StickerSetKind.Mask,
            _ => null,
        };
        using TLDocument? document = collection == StickerCollection.SavedGifs
            ? await _documents.GetDocumentAsync(id, accessHash)
            : await _documents.FindStickerDocumentAsync(id, accessHash,
                requiredKind);
        if (document is null || collection == StickerCollection.SavedGifs &&
            !StickerRows.IsAnimated(document.Value.AsDocument()))
        {
            return StickerResults.Error("STICKER_ID_INVALID");
        }

        StickerAccountSnapshot account = await _accounts.ReadAsync(userId);
        int now = checked((int)_timeProvider.GetUtcNow().ToUnixTimeSeconds());
        switch (collection)
        {
            case StickerCollection.SavedGifs:
                account = account with
                {
                    SavedGifs = remove
                        ? StickerAccountStore.Remove(account.SavedGifs, id)
                        : StickerAccountStore.Prepend(account.SavedGifs, id),
                };
                break;
            case StickerCollection.Recent:
                (long[] recent, int[] recentDates) = StickerAccountStore
                    .MoveRecent(account.Recent, account.RecentDates, id, now,
                        remove);
                account = account with
                {
                    Recent = recent, RecentDates = recentDates,
                };
                break;
            case StickerCollection.AttachedRecent:
                (long[] attached, int[] attachedDates) = StickerAccountStore
                    .MoveRecent(account.AttachedRecent,
                        account.AttachedRecentDates, id, now, remove);
                account = account with
                {
                    AttachedRecent = attached,
                    AttachedRecentDates = attachedDates,
                };
                break;
            case StickerCollection.Faved:
                account = account with
                {
                    Faved = remove
                        ? StickerAccountStore.Remove(account.Faved, id)
                        : StickerAccountStore.Prepend(account.Faved, id),
                };
                break;
            default:
                return StickerResults.Error("STICKER_ID_INVALID");
        }
        if (!await _accounts.PersistAsync(userId, account))
        {
            return StickerResults.StorageError();
        }
        await _notifier.NotifyCollectionAsync(userId, authKeyId, collection);
        return StickerResults.True();
    }

    public async ValueTask<TLBytes> ClearRecentAsync(long userId,
        long authKeyId, bool attached)
    {
        StickerAccountSnapshot account = await _accounts.ReadAsync(userId);
        account = attached
            ? account with { AttachedRecent = [], AttachedRecentDates = [] }
            : account with { Recent = [], RecentDates = [] };
        if (!await _accounts.PersistAsync(userId, account))
        {
            return StickerResults.StorageError();
        }
        await _notifier.NotifyCollectionAsync(userId, authKeyId,
            attached ? StickerCollection.AttachedRecent
                : StickerCollection.Recent);
        return StickerResults.True();
    }

    public async ValueTask<TLBytes> GetSavedGifsAsync(long userId,
        long requestedHash)
    {
        StickerAccountSnapshot account = await _accounts.ReadAsync(userId);
        long[] ids = account.SavedGifs;
        long hash = StickerRows.HashIds(ids);
        if (requestedHash != 0 && requestedHash == hash)
        {
            var unchanged = SavedGifsNotModified.Builder().Build();
            return unchanged.TLBytes!.Value;
        }
        IReadOnlyCollection<TLStickerSetState> rows =
            await _repository.GetSetsAsync();
        try
        {
            Vector documents = _documents.BuildDocuments(ids, rows,
                includeGeneral: true);
            var result = SavedGifs.Builder().Hash(hash).Gifs(documents).Build();
            return result.TLBytes!.Value;
        }
        finally
        {
            StickerRows.Dispose(rows);
        }
    }

    public async ValueTask<TLBytes> GetRecentAsync(long userId, bool attached,
        long requestedHash)
    {
        StickerAccountSnapshot account = await _accounts.ReadAsync(userId);
        long[] ids = attached ? account.AttachedRecent : account.Recent;
        int[] dates = attached ? account.AttachedRecentDates
            : account.RecentDates;
        long hash = StickerRows.HashIds(ids);
        if (requestedHash != 0 && requestedHash == hash)
        {
            var unchanged = RecentStickersNotModified.Builder().Build();
            return unchanged.TLBytes!.Value;
        }
        IReadOnlyCollection<TLStickerSetState> rows =
            await _repository.GetSetsAsync();
        try
        {
            var result = RecentStickers.Builder().Hash(hash)
                .Packs(StickerRows.BuildPacks(ids, rows))
                .Stickers(_documents.BuildDocuments(ids, rows))
                .Dates(StickerVectors.ToIntVector(dates)).Build();
            return result.TLBytes!.Value;
        }
        finally
        {
            StickerRows.Dispose(rows);
        }
    }

    public async ValueTask<TLBytes> GetFavedAsync(long userId,
        long requestedHash)
    {
        StickerAccountSnapshot account = await _accounts.ReadAsync(userId);
        long[] ids = account.Faved;
        long hash = StickerRows.HashIds(ids);
        if (requestedHash != 0 && requestedHash == hash)
        {
            var unchanged = FavedStickersNotModified.Builder().Build();
            return unchanged.TLBytes!.Value;
        }
        IReadOnlyCollection<TLStickerSetState> rows =
            await _repository.GetSetsAsync();
        try
        {
            var result = FavedStickers.Builder().Hash(hash)
                .Packs(StickerRows.BuildPacks(ids, rows))
                .Stickers(_documents.BuildDocuments(ids, rows))
                .Build();
            return result.TLBytes!.Value;
        }
        finally
        {
            StickerRows.Dispose(rows);
        }
    }
}
