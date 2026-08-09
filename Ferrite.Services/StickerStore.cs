// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Crypto;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services;

public enum StickerSetKind
{
    Regular,
    Mask,
    Emoji,
}

public sealed partial class StickerStore
{
    private readonly IDocumentsRepository _documentsRepository;
    private readonly IStickerRepository _stickerRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IStickerRepository _repository;
    private readonly IUpdatesService _updates;
    private readonly TimeProvider _timeProvider;
    private readonly IRandomGenerator _random;

    public StickerStore(IUnitOfWork unitOfWork, IDocumentsRepository documentsRepository, IStickerRepository stickerRepository, IUpdatesService updates,
        TimeProvider timeProvider, IRandomGenerator random)
    {
        _documentsRepository = documentsRepository;
        _stickerRepository = stickerRepository;

        _unitOfWork = unitOfWork;
        _repository = stickerRepository;
        _updates = updates;
        _timeProvider = timeProvider;
        _random = random;
    }

    public async ValueTask<TLBytes> GetInstalledAsync(long userId,
        StickerSetKind kind, long requestedHash)
    {
        long[] installed = await ReadAccountIdsAsync(userId,
            archived: false);
        IReadOnlyCollection<TLStickerSetState> rows =
            await _repository.GetSetsAsync();
        try
        {
            Dictionary<long, TLStickerSetState> byId = rows.ToDictionary(
                row => row.AsStickerSetState().SetId);
            List<TLStickerSetState> selected = installed
                .Where(byId.ContainsKey).Select(id => byId[id])
                .Where(row => MatchesKind(row.AsStickerSetState().Get_SetView()
                    .AsStickerSet(), kind))
                .ToList();
            long hash = Hash(selected);
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
            Dispose(rows);
        }
    }

    public async ValueTask<TLBytes> GetArchivedAsync(long userId,
        StickerSetKind kind, long offsetId, int limit)
    {
        long[] archived = await ReadAccountIdsAsync(userId,
            archived: true);
        IReadOnlyCollection<TLStickerSetState> rows =
            await _repository.GetSetsAsync();
        try
        {
            Dictionary<long, TLStickerSetState> byId = rows.ToDictionary(
                row => row.AsStickerSetState().SetId);
            List<TLStickerSetState> all = archived
                .Where(byId.ContainsKey).Select(id => byId[id])
                .Where(row => MatchesKind(row.AsStickerSetState().Get_SetView()
                    .AsStickerSet(), kind))
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
            Dispose(rows);
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
                MatchesKind(row.AsStickerSetState().Get_SetView().AsStickerSet(),
                    kind) &&
                MatchesQuery(row.AsStickerSetState().Get_SetView().AsStickerSet(),
                    search))
                .OrderBy(row => row.AsStickerSetState().SetId).ToList();
            long hash = Hash(selected);
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
            Dispose(rows);
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
            Dispose(rows);
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
            .Packs(CopyObjectVector(view.Packs))
            .Keywords(CopyObjectVector(view.Keywords))
            .Documents(CopyObjectVector(view.Documents)).Build();
        return result.TLBytes!.Value;
    }

    public static (long? Id, long? AccessHash, string? ShortName) ReadInputSet(
        InputStickerSetView input)
    {
        if (input.Is(out InputStickerSetID id))
        {
            return (id.Id, id.AccessHash, null);
        }
        if (input.Is(out InputStickerSetShortName named))
        {
            return (null, null, Encoding.UTF8.GetString(named.ShortName));
        }
        return (null, null, null);
    }

    public static (long? Id, long? AccessHash) ReadInputDocument(
        InputDocumentView input)
    {
        return input.Is(out InputDocument document)
            ? (document.Id, document.AccessHash)
            : (null, null);
    }

    public async ValueTask<TLDocument?> GetDocumentAsync(long id,
        long accessHash)
    {
        IReadOnlyCollection<TLStickerSetState> rows =
            await _repository.GetSetsAsync();
        try
        {
            foreach (TLStickerSetState row in rows)
            {
                Vector documents = row.AsStickerSetState().Documents;
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
        }
        finally
        {
            Dispose(rows);
        }

        using TLBytes? stored = _documentsRepository.GetDocument(id);
        if (stored is null)
        {
            return null;
        }
        var documentView = (DocumentView)stored.Value.AsSpan();
        return documentView.Is(out Document storedDocument) &&
               storedDocument.AccessHash == accessHash
            ? storedDocument.Clone().Build()
            : null;
    }

    private async ValueTask<long[]> ReadAccountIdsAsync(long userId,
        bool archived)
    {
        using TLStickerAccountState? state =
            await _repository.GetAccountStateAsync(userId);
        if (state is null)
        {
            return [];
        }
        var view = state.Value.AsStickerAccountState();
        return (archived ? view.Archived : view.Installed).ToArray();
    }

    private static bool MatchesKind(StickerSet set, StickerSetKind kind) =>
        kind switch
        {
            StickerSetKind.Mask => set.Masks,
            StickerSetKind.Emoji => set.Emojis,
            _ => !set.Masks && !set.Emojis,
        };

    private static bool MatchesQuery(StickerSet set, string query)
    {
        if (query.Length == 0)
        {
            return true;
        }
        return Encoding.UTF8.GetString(set.Title).Contains(query,
                   StringComparison.OrdinalIgnoreCase) ||
               Encoding.UTF8.GetString(set.ShortName).Contains(query,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static long Hash(IEnumerable<TLStickerSetState> rows)
    {
        long hash = 1;
        foreach (TLStickerSetState row in rows)
        {
            var view = row.AsStickerSetState();
            hash = unchecked(hash * 20261 + view.SetId * 31 + view.Revision);
        }
        return hash;
    }

    private static void Dispose(IEnumerable<TLStickerSetState> rows)
    {
        foreach (TLStickerSetState row in rows)
        {
            row.Dispose();
        }
    }

    private static Vector CopyObjectVector(Vector source)
    {
        var result = new Vector();
        int count = source.Count;
        for (int i = 0; i < count; i++)
        {
            result.AppendTLObject(source.ReadTLObject());
        }
        return result;
    }
}
