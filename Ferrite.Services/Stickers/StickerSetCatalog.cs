// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Stickers;

public sealed class StickerSetCatalog
{
    private readonly IStickerRepository _repository;
    private readonly StickerAccountStore _accounts;

    public StickerSetCatalog(IStickerRepository repository,
        StickerAccountStore accounts)
    {
        _repository = repository;
        _accounts = accounts;
    }

    public async ValueTask<TLBytes> GetInstalledAsync(long userId,
        StickerSetKind kind, long requestedHash)
    {
        StickerAccountSnapshot account = await _accounts.ReadAsync(userId);
        long[] installed = account.Installed;
        IReadOnlyCollection<TLStickerSetState> rows =
            await _repository.GetSetsAsync();
        try
        {
            Dictionary<long, TLStickerSetState> byId = rows.ToDictionary(
                row => row.AsStickerSetState().SetId);
            List<TLStickerSetState> selected = installed
                .Where(byId.ContainsKey).Select(id => byId[id])
                .Where(row => StickerRows.MatchesKind(row.AsStickerSetState()
                    .Get_SetView().AsStickerSet(), kind))
                .ToList();
            long hash = StickerRows.Hash(selected);
            if (requestedHash != 0 && requestedHash == hash)
            {
                var unchanged = AllStickersNotModified.Builder().Build();
                return unchanged.TLBytes!.Value;
            }

            var sets = new Vector();
            foreach (TLStickerSetState row in selected)
            {
                sets.AppendTLObject(row.AsStickerSetState().Get_SetView()
                    .AsStickerSet().ToReadOnlySpan());
            }
            var result = AllStickers.Builder().Hash(hash)
                .Sets(sets).Build();
            return result.TLBytes!.Value;
        }
        finally
        {
            StickerRows.Dispose(rows);
        }
    }

    public async ValueTask<TLBytes> GetArchivedAsync(long userId,
        StickerSetKind kind, long offsetId, int limit)
    {
        StickerAccountSnapshot account = await _accounts.ReadAsync(userId);
        long[] archived = account.Archived;
        IReadOnlyCollection<TLStickerSetState> rows =
            await _repository.GetSetsAsync();
        try
        {
            Dictionary<long, TLStickerSetState> byId = rows.ToDictionary(
                row => row.AsStickerSetState().SetId);
            List<TLStickerSetState> all = archived
                .Where(byId.ContainsKey).Select(id => byId[id])
                .Where(row => StickerRows.MatchesKind(row.AsStickerSetState()
                    .Get_SetView().AsStickerSet(), kind))
                .ToList();
            IEnumerable<TLStickerSetState> page = all;
            if (offsetId != 0)
            {
                page = page.SkipWhile(row =>
                    row.AsStickerSetState().SetId != offsetId).Skip(1);
            }
            var sets = new Vector();
            foreach (TLStickerSetState row in page.Take(Math.Clamp(limit, 0, 200)))
            {
                using var covered = StickerSetNoCovered.Builder()
                    .Set(row.AsStickerSetState().Get_SetView().AsStickerSet()
                        .ToReadOnlySpan()).Build();
                sets.AppendTLObject(covered.ToReadOnlySpan());
            }
            var result = ArchivedStickers.Builder().Count(all.Count)
                .Sets(sets).Build();
            return result.TLBytes!.Value;
        }
        finally
        {
            StickerRows.Dispose(rows);
        }
    }

    public static TLBytes EmptyFeatured(long requestedHash)
    {
        if (requestedHash == 1)
        {
            var unchanged = FeaturedStickersNotModified.Builder()
                .Count(0).Build();
            return unchanged.TLBytes!.Value;
        }
        var result = FeaturedStickers.Builder().Hash(1).Count(0)
            .Sets(new Vector()).Unread(new VectorOfLong()).Build();
        return result.TLBytes!.Value;
    }

    public async ValueTask<TLBytes> SearchSetsAsync(StickerSetKind kind,
        ReadOnlyMemory<byte> query, long requestedHash)
    {
        string search = Encoding.UTF8.GetString(query.Span);
        IReadOnlyCollection<TLStickerSetState> rows =
            await _repository.GetSetsAsync();
        try
        {
            List<TLStickerSetState> selected = rows.Where(row =>
                StickerRows.MatchesKind(row.AsStickerSetState().Get_SetView()
                    .AsStickerSet(), kind) &&
                StickerRows.MatchesQuery(row.AsStickerSetState().Get_SetView()
                    .AsStickerSet(), search))
                .OrderBy(row => row.AsStickerSetState().SetId).ToList();
            long hash = StickerRows.Hash(selected);
            if (requestedHash != 0 && requestedHash == hash)
            {
                var unchanged = FoundStickerSetsNotModified.Builder()
                    .Build();
                return unchanged.TLBytes!.Value;
            }
            var sets = new Vector();
            foreach (TLStickerSetState row in selected)
            {
                using var covered = StickerSetNoCovered.Builder()
                    .Set(row.AsStickerSetState().Get_SetView().AsStickerSet()
                        .ToReadOnlySpan()).Build();
                sets.AppendTLObject(covered.ToReadOnlySpan());
            }
            var result = FoundStickerSets.Builder().Hash(hash)
                .Sets(sets).Build();
            return result.TLBytes!.Value;
        }
        finally
        {
            StickerRows.Dispose(rows);
        }
    }

    public async ValueTask<TLBytes> GetOwnedAsync(long userId, long offsetId,
        int limit)
    {
        IReadOnlyCollection<TLStickerSetState> rows =
            await _repository.GetOwnedSetsAsync(userId);
        try
        {
            List<TLStickerSetState> ordered = rows
                .Where(row => offsetId == 0 ||
                              row.AsStickerSetState().SetId > offsetId)
                .OrderBy(row => row.AsStickerSetState().SetId).ToList();
            var sets = new Vector();
            foreach (TLStickerSetState row in ordered.Take(Math.Clamp(limit, 0, 200)))
            {
                using var covered = StickerSetNoCovered.Builder()
                    .Set(row.AsStickerSetState().Get_SetView().AsStickerSet()
                        .ToReadOnlySpan()).Build();
                sets.AppendTLObject(covered.ToReadOnlySpan());
            }
            var result = MyStickers.Builder().Count(rows.Count)
                .Sets(sets).Build();
            return result.TLBytes!.Value;
        }
        finally
        {
            StickerRows.Dispose(rows);
        }
    }

    public async ValueTask<TLBytes?> GetFullSetAsync(long? setId,
        long? accessHash, string? shortName)
    {
        using TLStickerSetState? row = setId.HasValue
            ? await _repository.GetSetAsync(setId.Value)
            : shortName is not null
                ? await _repository.GetSetByShortNameAsync(shortName)
                : null;
        if (row is null)
        {
            return null;
        }
        var view = row.Value.AsStickerSetState();
        if (setId.HasValue && accessHash.HasValue &&
            view.Get_SetView().AsStickerSet().AccessHash != accessHash.Value)
        {
            return null;
        }
        var result = MessagesStickerSet.Builder()
            .Set(view.Get_SetView().AsStickerSet().ToReadOnlySpan())
            .Packs(StickerVectors.CopyObjectVector(view.Packs))
            .Keywords(StickerVectors.CopyObjectVector(view.Keywords))
            .Documents(StickerVectors.CopyObjectVector(view.Documents)).Build();
        return result.TLBytes!.Value;
    }
}
