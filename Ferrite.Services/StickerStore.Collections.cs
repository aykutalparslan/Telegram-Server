// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services;

public sealed partial class StickerStore
{
    private const int CollectionLimit = 200;

    public enum AccountCollection
    {
        SavedGifs,
        Recent,
        AttachedRecent,
        Faved,
        FeaturedRead,
    }

    public async ValueTask<long[]?> ResolveSetIdsAsync(
        IReadOnlyList<(long? Id, long? AccessHash, string? ShortName)> inputs)
    {
        var ids = new long[inputs.Count];
        for (int i = 0; i < inputs.Count; i++)
        {
            (long? id, long? accessHash, string? shortName) = inputs[i];
            using TLStickerSetState? row = await ResolveSetAsync(id, accessHash,
                shortName);
            if (row is null) return null;
            ids[i] = row.Value.AsStickerSetState().SetId;
        }
        return ids;
    }

    public async ValueTask<TLBytes> InstallAsync(long userId, long authKeyId,
        long? setId, long? accessHash, string? shortName, bool archived)
    {
        using TLStickerSetState? set = await ResolveSetAsync(setId, accessHash,
            shortName);
        if (set is null)
        {
            return Error("STICKERSET_INVALID");
        }

        long id = set.Value.AsStickerSetState().SetId;
        using TLStickerAccountState? state =
            await _repository.GetAccountStateAsync(userId);
        ReadAccount(state, out long revision, out long[] installed,
            out long[] archivedIds, out long[] savedGifs, out long[] recent,
            out int[] recentDates, out long[] attachedRecent,
            out int[] attachedRecentDates, out long[] faved,
            out long[] featuredRead);
        long[] displaced = [];
        if (archived)
        {
            installed = Remove(installed, id);
            archivedIds = Prepend(archivedIds, id);
        }
        else
        {
            archivedIds = Remove(archivedIds, id);
            displaced = installed.Where(value => value != id)
                .Skip(CollectionLimit - 1).ToArray();
            foreach (long displacedId in displaced)
            {
                archivedIds = Prepend(archivedIds, displacedId);
            }
            installed = Prepend(installed, id);
        }
        if (!await PersistAccountAsync(userId, revision + 1, installed,
                archivedIds, savedGifs, recent, recentDates, attachedRecent,
                attachedRecentDates, faved, featuredRead))
        {
            return StorageError();
        }
        await NotifyStickerSetsAsync(userId, authKeyId,
            Kind(set.Value.AsStickerSetState().Get_SetView().AsStickerSet()));
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
                Dispose(rows);
            }
        }
        var result = StickerSetInstallResultSuccess.Builder().Build();
        return result.TLBytes!.Value;
    }

    public async ValueTask<TLBytes> UninstallAsync(long userId, long authKeyId,
        long? setId, long? accessHash, string? shortName)
    {
        using TLStickerSetState? set = await ResolveSetAsync(setId, accessHash,
            shortName);
        if (set is null)
        {
            return Error("STICKERSET_INVALID");
        }
        long id = set.Value.AsStickerSetState().SetId;
        using TLStickerAccountState? state =
            await _repository.GetAccountStateAsync(userId);
        ReadAccount(state, out long revision, out long[] installed,
            out long[] archived, out long[] savedGifs, out long[] recent,
            out int[] recentDates, out long[] attachedRecent,
            out int[] attachedRecentDates, out long[] faved,
            out long[] featuredRead);
        installed = Remove(installed, id);
        archived = Remove(archived, id);
        if (!await PersistAccountAsync(userId, revision + 1, installed, archived,
                savedGifs, recent, recentDates, attachedRecent,
                attachedRecentDates, faved, featuredRead))
        {
            return StorageError();
        }
        await NotifyStickerSetsAsync(userId, authKeyId,
            Kind(set.Value.AsStickerSetState().Get_SetView().AsStickerSet()));
        return True();
    }

    public async ValueTask<TLBytes> ToggleSetsAsync(long userId, long authKeyId,
        long[] setIds, bool uninstall, bool archive, bool unarchive)
    {
        if ((uninstall ? 1 : 0) + (archive ? 1 : 0) +
            (unarchive ? 1 : 0) != 1)
        {
            return Error("STICKERSET_INVALID");
        }
        var kinds = new HashSet<StickerSetKind>();
        foreach (long id in setIds)
        {
            using TLStickerSetState? row = await _repository.GetSetAsync(id);
            if (row is null)
            {
                return Error("STICKERSET_INVALID");
            }
            kinds.Add(Kind(row.Value.AsStickerSetState().Get_SetView()
                .AsStickerSet()));
        }

        using TLStickerAccountState? state =
            await _repository.GetAccountStateAsync(userId);
        ReadAccount(state, out long revision, out long[] installed,
            out long[] archivedIds, out long[] savedGifs, out long[] recent,
            out int[] recentDates, out long[] attachedRecent,
            out int[] attachedRecentDates, out long[] faved,
            out long[] featuredRead);
        foreach (long id in setIds)
        {
            if (uninstall)
            {
                installed = Remove(installed, id);
                archivedIds = Remove(archivedIds, id);
            }
            else if (archive)
            {
                installed = Remove(installed, id);
                archivedIds = Prepend(archivedIds, id);
            }
            else
            {
                archivedIds = Remove(archivedIds, id);
                installed = Prepend(installed, id);
            }
        }
        if (!await PersistAccountAsync(userId, revision + 1, installed,
                archivedIds, savedGifs, recent, recentDates, attachedRecent,
                attachedRecentDates, faved, featuredRead))
        {
            return StorageError();
        }
        foreach (StickerSetKind kind in kinds)
        {
            await NotifyStickerSetsAsync(userId, authKeyId, kind);
        }
        return True();
    }

    public async ValueTask<TLBytes> ReorderAsync(long userId, long authKeyId,
        StickerSetKind kind, long[] order)
    {
        if (order.Length != order.Distinct().Count())
        {
            return Error("STICKERSET_INVALID");
        }
        using TLStickerAccountState? state =
            await _repository.GetAccountStateAsync(userId);
        ReadAccount(state, out long revision, out long[] installed,
            out long[] archived, out long[] savedGifs, out long[] recent,
            out int[] recentDates, out long[] attachedRecent,
            out int[] attachedRecentDates, out long[] faved,
            out long[] featuredRead);

        var matching = new List<long>();
        var other = new List<long>();
        foreach (long id in installed)
        {
            using TLStickerSetState? row = await _repository.GetSetAsync(id);
            if (row is not null && Kind(row.Value.AsStickerSetState()
                    .Get_SetView().AsStickerSet()) == kind)
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
            return Error("STICKERSET_INVALID");
        }
        var reordered = new List<long>(installed.Length);
        int matchingIndex = 0;
        int otherIndex = 0;
        foreach (long id in installed)
        {
            using TLStickerSetState? row = await _repository.GetSetAsync(id);
            bool isKind = row is not null && Kind(row.Value.AsStickerSetState()
                .Get_SetView().AsStickerSet()) == kind;
            reordered.Add(isKind ? order[matchingIndex++] : other[otherIndex++]);
        }
        if (!await PersistAccountAsync(userId, revision + 1,
                reordered.ToArray(), archived, savedGifs, recent, recentDates,
                attachedRecent, attachedRecentDates, faved, featuredRead))
        {
            return StorageError();
        }
        await NotifyStickerSetsAsync(userId, authKeyId, kind);
        return True();
    }

    public async ValueTask<TLBytes> ReadFeaturedAsync(long userId,
        long authKeyId, long[] ids)
    {
        using TLStickerAccountState? state =
            await _repository.GetAccountStateAsync(userId);
        ReadAccount(state, out long revision, out long[] installed,
            out long[] archived, out long[] savedGifs, out long[] recent,
            out int[] recentDates, out long[] attachedRecent,
            out int[] attachedRecentDates, out long[] faved,
            out long[] featuredRead);
        foreach (long id in ids)
        {
            featuredRead = Prepend(featuredRead, id);
        }
        if (!await PersistAccountAsync(userId, revision + 1, installed, archived,
                savedGifs, recent, recentDates, attachedRecent,
                attachedRecentDates, faved, featuredRead))
        {
            return StorageError();
        }
        using TLUpdate update = UpdateReadFeaturedStickers.Builder().Build();
        await _updates.EnqueueUpdate(userId, update,
            UpdateDeliveryScope.ExcludingAuthKeys([authKeyId]));
        return True();
    }

    public async ValueTask<TLBytes> SaveCollectionDocumentAsync(long userId,
        long authKeyId, long id, long accessHash, AccountCollection collection,
        bool remove)
    {
        StickerSetKind? requiredKind = collection switch
        {
            AccountCollection.Recent => StickerSetKind.Regular,
            AccountCollection.AttachedRecent => StickerSetKind.Mask,
            _ => null,
        };
        using TLDocument? document = collection == AccountCollection.SavedGifs
            ? await GetDocumentAsync(id, accessHash)
            : await FindStickerDocumentAsync(id, accessHash, requiredKind);
        if (document is null || collection == AccountCollection.SavedGifs &&
            !IsAnimated(document.Value.AsDocument()))
        {
            return Error("STICKER_ID_INVALID");
        }

        using TLStickerAccountState? state =
            await _repository.GetAccountStateAsync(userId);
        ReadAccount(state, out long revision, out long[] installed,
            out long[] archived, out long[] savedGifs, out long[] recent,
            out int[] recentDates, out long[] attachedRecent,
            out int[] attachedRecentDates, out long[] faved,
            out long[] featuredRead);
        int now = checked((int)_timeProvider.GetUtcNow().ToUnixTimeSeconds());
        switch (collection)
        {
            case AccountCollection.SavedGifs:
                savedGifs = remove ? Remove(savedGifs, id) : Prepend(savedGifs, id);
                break;
            case AccountCollection.Recent:
                MoveRecent(recent, recentDates, id, now, remove,
                    out recent, out recentDates);
                break;
            case AccountCollection.AttachedRecent:
                MoveRecent(attachedRecent, attachedRecentDates, id, now, remove,
                    out attachedRecent, out attachedRecentDates);
                break;
            case AccountCollection.Faved:
                faved = remove ? Remove(faved, id) : Prepend(faved, id);
                break;
            default:
                return Error("STICKER_ID_INVALID");
        }
        if (!await PersistAccountAsync(userId, revision + 1, installed, archived,
                savedGifs, recent, recentDates, attachedRecent,
                attachedRecentDates, faved, featuredRead))
        {
            return StorageError();
        }
        await NotifyCollectionAsync(userId, authKeyId, collection);
        return True();
    }

    public async ValueTask<TLBytes> ClearRecentAsync(long userId,
        long authKeyId, bool attached)
    {
        using TLStickerAccountState? state =
            await _repository.GetAccountStateAsync(userId);
        ReadAccount(state, out long revision, out long[] installed,
            out long[] archived, out long[] savedGifs, out long[] recent,
            out int[] recentDates, out long[] attachedRecent,
            out int[] attachedRecentDates, out long[] faved,
            out long[] featuredRead);
        if (attached)
        {
            attachedRecent = [];
            attachedRecentDates = [];
        }
        else
        {
            recent = [];
            recentDates = [];
        }
        if (!await PersistAccountAsync(userId, revision + 1, installed, archived,
                savedGifs, recent, recentDates, attachedRecent,
                attachedRecentDates, faved, featuredRead))
        {
            return StorageError();
        }
        await NotifyCollectionAsync(userId, authKeyId,
            attached ? AccountCollection.AttachedRecent : AccountCollection.Recent);
        return True();
    }

    public async ValueTask<TLBytes> GetSavedGifsAsync(long userId,
        long requestedHash)
    {
        using TLStickerAccountState? state =
            await _repository.GetAccountStateAsync(userId);
        long[] ids = state is null ? [] : state.Value.AsStickerAccountState()
            .SavedGifs.ToArray();
        long hash = HashIds(ids);
        if (requestedHash != 0 && requestedHash == hash)
        {
            var unchanged = SavedGifsNotModified.Builder().Build();
            return unchanged.TLBytes!.Value;
        }
        IReadOnlyCollection<TLStickerSetState> rows =
            await _repository.GetSetsAsync();
        try
        {
            Vector documents = BuildDocuments(ids, rows, includeGeneral: true);
            var result = SavedGifs.Builder().Hash(hash).Gifs(documents).Build();
            return result.TLBytes!.Value;
        }
        finally
        {
            Dispose(rows);
        }
    }

    public async ValueTask<TLBytes> GetRecentAsync(long userId, bool attached,
        long requestedHash)
    {
        using TLStickerAccountState? state =
            await _repository.GetAccountStateAsync(userId);
        long[] ids = state is null ? [] : (attached
            ? state.Value.AsStickerAccountState().AttachedRecent
            : state.Value.AsStickerAccountState().Recent).ToArray();
        int[] dates = state is null ? [] : (attached
            ? state.Value.AsStickerAccountState().AttachedRecentDates
            : state.Value.AsStickerAccountState().RecentDates).ToArray();
        long hash = HashIds(ids);
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
                .Packs(BuildPacks(ids, rows)).Stickers(BuildDocuments(ids, rows))
                .Dates(ToIntVector(dates)).Build();
            return result.TLBytes!.Value;
        }
        finally
        {
            Dispose(rows);
        }
    }

    public async ValueTask<TLBytes> GetFavedAsync(long userId,
        long requestedHash)
    {
        using TLStickerAccountState? state =
            await _repository.GetAccountStateAsync(userId);
        long[] ids = state is null ? [] : state.Value.AsStickerAccountState()
            .Faved.ToArray();
        long hash = HashIds(ids);
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
                .Packs(BuildPacks(ids, rows)).Stickers(BuildDocuments(ids, rows))
                .Build();
            return result.TLBytes!.Value;
        }
        finally
        {
            Dispose(rows);
        }
    }

    private async ValueTask<TLStickerSetState?> ResolveSetAsync(long? id,
        long? accessHash, string? shortName)
    {
        TLStickerSetState? row = id.HasValue
            ? await _repository.GetSetAsync(id.Value)
            : shortName is not null
                ? await _repository.GetSetByShortNameAsync(shortName)
                : null;
        if (row is null)
        {
            return null;
        }
        if (id.HasValue && (!accessHash.HasValue ||
            row.Value.AsStickerSetState().Get_SetView().AsStickerSet()
                .AccessHash != accessHash.Value))
        {
            row.Value.Dispose();
            return null;
        }
        return row;
    }

    private async ValueTask<TLDocument?> FindStickerDocumentAsync(long id,
        long accessHash, StickerSetKind? requiredKind)
    {
        IReadOnlyCollection<TLStickerSetState> rows =
            await _repository.GetSetsAsync();
        try
        {
            foreach (TLStickerSetState row in rows)
            {
                var set = row.AsStickerSetState();
                if (requiredKind.HasValue &&
                    Kind(set.Get_SetView().AsStickerSet()) != requiredKind.Value)
                {
                    continue;
                }
                Vector documents = set.Documents;
                int count = documents.Count;
                for (int i = 0; i < count; i++)
                {
                    var document = (DocumentView)documents.ReadTLObject();
                    if (document.Is(out Document value) && value.Id == id &&
                        value.AccessHash == accessHash)
                    {
                        return value.Clone().Build();
                    }
                }
            }
            return null;
        }
        finally
        {
            Dispose(rows);
        }
    }

    private async ValueTask<bool> PersistAccountAsync(long userId,
        long revision, long[] installed, long[] archived, long[] savedGifs,
        long[] recent, int[] recentDates, long[] attachedRecent,
        int[] attachedRecentDates, long[] faved, long[] featuredRead)
    {
        using TLStickerAccountState state = StickerAccountState.Builder()
            .UserId(userId).Revision(revision).Installed(ToLongVector(installed))
            .Archived(ToLongVector(archived)).SavedGifs(ToLongVector(savedGifs))
            .Recent(ToLongVector(recent)).RecentDates(ToIntVector(recentDates))
            .AttachedRecent(ToLongVector(attachedRecent))
            .AttachedRecentDates(ToIntVector(attachedRecentDates))
            .Faved(ToLongVector(faved)).FeaturedRead(ToLongVector(featuredRead))
            .Build();
        return _repository.PutAccountState(state) &&
               await _unitOfWork.SaveAsync();
    }

    private static void ReadAccount(TLStickerAccountState? state,
        out long revision, out long[] installed, out long[] archived,
        out long[] savedGifs, out long[] recent, out int[] recentDates,
        out long[] attachedRecent, out int[] attachedRecentDates,
        out long[] faved, out long[] featuredRead)
    {
        if (state is null)
        {
            revision = 0;
            installed = []; archived = []; savedGifs = []; recent = [];
            recentDates = []; attachedRecent = []; attachedRecentDates = [];
            faved = []; featuredRead = [];
            return;
        }
        var view = state.Value.AsStickerAccountState();
        revision = view.Revision;
        installed = view.Installed.ToArray();
        archived = view.Archived.ToArray();
        savedGifs = view.SavedGifs.ToArray();
        recent = view.Recent.ToArray();
        recentDates = view.RecentDates.ToArray();
        attachedRecent = view.AttachedRecent.ToArray();
        attachedRecentDates = view.AttachedRecentDates.ToArray();
        faved = view.Faved.ToArray();
        featuredRead = view.FeaturedRead.ToArray();
    }

    private static long[] Prepend(long[] source, long id) =>
        [id, .. source.Where(value => value != id).Take(CollectionLimit - 1)];

    private static long[] Remove(long[] source, long id) =>
        source.Where(value => value != id).ToArray();

    private static void MoveRecent(long[] ids, int[] dates, long id, int now,
        bool remove, out long[] resultIds, out int[] resultDates)
    {
        var entries = ids.Select((value, index) => (Id: value,
                Date: index < dates.Length ? dates[index] : 0))
            .Where(entry => entry.Id != id).ToList();
        if (!remove)
        {
            entries.Insert(0, (id, now));
        }
        resultIds = entries.Take(CollectionLimit).Select(x => x.Id).ToArray();
        resultDates = entries.Take(CollectionLimit).Select(x => x.Date).ToArray();
    }

    private static VectorOfLong ToLongVector(IEnumerable<long> values)
    {
        var vector = new VectorOfLong();
        foreach (long value in values) vector.Append(value);
        return vector;
    }

    private static VectorOfInt ToIntVector(IEnumerable<int> values)
    {
        var vector = new VectorOfInt();
        foreach (int value in values) vector.Append(value);
        return vector;
    }

    private Vector BuildDocuments(IEnumerable<long> ids,
        IReadOnlyCollection<TLStickerSetState> rows, bool includeGeneral = false,
        StickerSetKind? requiredKind = null)
    {
        var result = new Vector();
        foreach (long id in ids)
        {
            bool found = false;
            foreach (TLStickerSetState row in rows)
            {
                if (requiredKind.HasValue && Kind(row.AsStickerSetState()
                        .Get_SetView().AsStickerSet()) != requiredKind.Value)
                {
                    continue;
                }
                Vector documents = row.AsStickerSetState().Documents;
                int count = documents.Count;
                for (int i = 0; i < count; i++)
                {
                    Span<byte> bytes = documents.ReadTLObject();
                    var document = (DocumentView)bytes;
                    if (document.Is(out Document value) && value.Id == id)
                    {
                        result.AppendTLObject(bytes);
                        found = true;
                        break;
                    }
                }
                if (found) break;
            }
            if (!found && includeGeneral)
            {
                using TLBytes? stored =
                    _documentsRepository.GetDocument(id);
                if (stored is not null)
                {
                    result.AppendTLObject(stored.Value.AsSpan());
                }
            }
        }
        return result;
    }

    private static Vector BuildPacks(IEnumerable<long> ids,
        IReadOnlyCollection<TLStickerSetState> rows)
    {
        HashSet<long> wanted = ids.ToHashSet();
        var result = new Vector();
        foreach (TLStickerSetState row in rows)
        {
            Vector packs = row.AsStickerSetState().Packs;
            int count = packs.Count;
            for (int i = 0; i < count; i++)
            {
                Span<byte> bytes = packs.ReadTLObject();
                var pack = (StickerPack)bytes;
                if (pack.Documents.ToArray().Any(wanted.Contains))
                {
                    result.AppendTLObject(bytes);
                }
            }
        }
        return result;
    }

    private static bool IsAnimated(Document document)
    {
        Vector attributes = document.Attributes;
        int count = attributes.Count;
        for (int i = 0; i < count; i++)
        {
            var attribute = (DocumentAttributeView)attributes.ReadTLObject();
            if (attribute.Is(out DocumentAttributeAnimated _)) return true;
        }
        return false;
    }

    private async ValueTask NotifyStickerSetsAsync(long userId, long authKeyId,
        StickerSetKind kind)
    {
        var builder = UpdateStickerSets.Builder();
        if (kind == StickerSetKind.Mask) builder = builder.Masks(true);
        if (kind == StickerSetKind.Emoji) builder = builder.Emojis(true);
        using TLUpdate update = builder.Build();
        await _updates.EnqueueUpdate(userId, update,
            UpdateDeliveryScope.ExcludingAuthKeys([authKeyId]));
    }

    private async ValueTask NotifyCollectionAsync(long userId, long authKeyId,
        AccountCollection collection)
    {
        using TLUpdate update = collection switch
        {
            AccountCollection.SavedGifs => UpdateSavedGifs.Builder().Build(),
            AccountCollection.Faved => UpdateFavedStickers.Builder().Build(),
            _ => UpdateRecentStickers.Builder().Build(),
        };
        await _updates.EnqueueUpdate(userId, update,
            UpdateDeliveryScope.ExcludingAuthKeys([authKeyId]));
    }

    private static StickerSetKind Kind(StickerSet set) => set.Emojis
        ? StickerSetKind.Emoji
        : set.Masks ? StickerSetKind.Mask : StickerSetKind.Regular;

    private static long HashIds(IEnumerable<long> ids)
    {
        long hash = 1;
        foreach (long id in ids) hash = unchecked(hash * 20261 + id);
        return hash;
    }

    private static TLBytes True()
    {
        TLBool result = BoolTrue.Builder().Build();
        return result.TLBytes;
    }

    private static TLBytes Error(string message) =>
        RpcErrorGenerator.GenerateError(400, Encoding.UTF8.GetBytes(message));

    private static TLBytes StorageError() =>
        RpcErrorGenerator.GenerateError(500, "STORAGE_FAILED"u8);
}
