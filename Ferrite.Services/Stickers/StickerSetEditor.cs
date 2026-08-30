// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using System.Text.RegularExpressions;
using Ferrite.Crypto;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Stickers;

public sealed class StickerSetEditor
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IStickerRepository _repository;
    private readonly StickerSetLookup _lookup;
    private readonly StickerDocumentIndex _documents;
    private readonly StickerAccountStore _accounts;
    private readonly IRandomGenerator _random;

    public StickerSetEditor(IUnitOfWork unitOfWork,
        IStickerRepository repository, StickerSetLookup lookup,
        StickerDocumentIndex documents, StickerAccountStore accounts,
        IRandomGenerator random)
    {
        _unitOfWork = unitOfWork;
        _repository = repository;
        _lookup = lookup;
        _documents = documents;
        _accounts = accounts;
        _random = random;
    }

    private static readonly Regex ShortNamePattern = new(
        "^[A-Za-z][A-Za-z0-9_]{0,63}$", RegexOptions.CultureInvariant);

    public async ValueTask<TLBytes> CheckShortNameAsync(string shortName)
    {
        if (!IsShortNameValid(shortName)) return StickerResults.False();
        using TLStickerSetState? existing =
            await _repository.GetSetByShortNameAsync(shortName);
        return existing is null ? StickerResults.True() : StickerResults.False();
    }

    public async ValueTask<TLBytes> SuggestShortNameAsync(string title)
    {
        string stem = new(title.ToLowerInvariant().Select(character =>
                char.IsAsciiLetterOrDigit(character) ? character : '_').ToArray());
        stem = stem.Trim('_');
        if (stem.Length == 0 || !char.IsAsciiLetter(stem[0])) stem = "sticker_" + stem;
        if (stem.Length > 56) stem = stem[..56];
        string candidate = stem;
        int suffix = 1;
        while (await _repository.GetSetByShortNameAsync(candidate) is { } existing)
        {
            existing.Dispose();
            candidate = $"{stem}_{suffix++}";
        }
        var result = Ferrite.TL.baseLayer.stickers.SuggestedShortName.Builder()
            .ShortName(Encoding.UTF8.GetBytes(candidate)).Build();
        return result.TLBytes!.Value;
    }

    public async ValueTask<TLBytes> CreateSetAsync(long userId, string title,
        string shortName, StickerSetKind kind, bool textColor,
        IReadOnlyList<StickerItemInput> items, (long Id, long AccessHash)? thumb)
    {
        if (title.Length is < 1 or > 64 || !IsShortNameValid(shortName) ||
            items.Count == 0 || textColor && kind != StickerSetKind.Emoji ||
            items.Any(item => !IsItemValid(kind, item)))
        {
            return StickerResults.Error(!IsShortNameValid(shortName)
                ? "SHORT_NAME_INVALID" : "STICKERSET_INVALID");
        }
        using (TLStickerSetState? occupied =
               await _repository.GetSetByShortNameAsync(shortName))
        {
            if (occupied is not null) return StickerResults.Error("SHORT_NAME_OCCUPIED");
        }

        long setId;
        do
        {
            setId = _random.NextLong() & long.MaxValue;
            if (setId == 0) continue;
            using TLStickerSetState? collision = await _repository.GetSetAsync(setId);
            if (collision is null) break;
            setId = 0;
        } while (true);
        long accessHash = _random.NextLong();

        var sources = new List<TLDocument>(items.Count);
        foreach (StickerItemInput item in items)
        {
            TLDocument? source = await _documents.GetDocumentAsync(item.DocumentId,
                item.AccessHash);
            if (source is null)
            {
                foreach (TLDocument value in sources) value.Dispose();
                return StickerResults.Error("STICKER_ID_INVALID");
            }
            sources.Add(source.Value);
        }

        TLBytes result;
        bool stored;
        try
        {
            long? thumbId = null;
            if (thumb.HasValue)
            {
                using TLDocument? thumbDocument = await _documents.GetDocumentAsync(
                    thumb.Value.Id, thumb.Value.AccessHash);
                if (thumbDocument is null) return StickerResults.Error("STICKER_ID_INVALID");
                thumbId = thumb.Value.Id;
            }
            (result, stored) = BuildCreatedSet(userId, setId,
                accessHash, title, shortName, kind, textColor, items, sources,
                thumbId);
        }
        finally
        {
            foreach (TLDocument value in sources) value.Dispose();
        }
        return await CommitResultAsync(result, stored);
    }

    public async ValueTask<TLBytes> AddAsync(long userId, long? setId,
        long? accessHash, string? shortName, StickerItemInput item)
    {
        using TLStickerSetState? row = await ResolveOwnedSetAsync(userId, setId,
            accessHash, shortName);
        if (row is null) return StickerResults.Error("STICKERSET_OWNER_ANONYMOUS");
        StickerSetKind kind = StickerRows.Kind(row.Value.AsStickerSetState().Get_SetView()
            .AsStickerSet());
        if (!IsItemValid(kind, item)) return StickerResults.Error("STICKER_INVALID");
        using TLDocument? source = await _documents.GetDocumentAsync(item.DocumentId,
            item.AccessHash);
        if (source is null) return StickerResults.Error("STICKER_ID_INVALID");
        var view = row.Value.AsStickerSetState();
        if (ContainsDocument(view.Documents, item.DocumentId))
            return StickerResults.Error("STICKER_ID_INVALID");
        StickerSet oldSet = view.Get_SetView().AsStickerSet();
        using TLDocument decorated = Decorate(source.Value.AsDocument(),
            view.SetId, oldSet.AccessHash, StickerRows.Kind(oldSet), oldSet.TextColor, item);
        Vector documents = StickerVectors.CopyObjectVector(view.Documents);
        documents.AppendTLObject(decorated.AsSpan());
        Vector packs = StickerVectors.CopyObjectVector(view.Packs);
        Vector keywords = StickerVectors.CopyObjectVector(view.Keywords);
        AppendItemMetadata(ref packs, ref keywords, item);
        TLBytes result = BuildMutation(view, oldSet, packs, keywords, documents,
            oldSet.Count + 1, out bool stored);
        return await CommitResultAsync(result, stored);
    }

    public async ValueTask<TLBytes> ReplaceAsync(long userId, long documentId,
        long documentAccessHash, StickerItemInput replacement)
    {
        using TLStickerSetState? row = await FindOwnedSetByDocumentAsync(userId,
            documentId, documentAccessHash);
        if (row is null) return StickerResults.Error("STICKER_ID_INVALID");
        using TLDocument? source = await _documents.GetDocumentAsync(replacement.DocumentId,
            replacement.AccessHash);
        if (source is null) return StickerResults.Error("STICKER_ID_INVALID");
        var view = row.Value.AsStickerSetState();
        StickerSet oldSet = view.Get_SetView().AsStickerSet();
        if (!IsItemValid(StickerRows.Kind(oldSet), replacement) ||
            replacement.DocumentId != documentId &&
            ContainsDocument(view.Documents, replacement.DocumentId))
            return StickerResults.Error("STICKER_INVALID");
        using TLDocument decorated = Decorate(source.Value.AsDocument(),
            view.SetId, oldSet.AccessHash, StickerRows.Kind(oldSet), oldSet.TextColor,
            replacement);
        Vector documents = ReplaceDocument(view.Documents, documentId,
            decorated.AsSpan());
        Vector packs = RemovePackMetadata(view.Packs, documentId);
        Vector keywords = RemoveKeywordMetadata(view.Keywords, documentId);
        AppendItemMetadata(ref packs, ref keywords, replacement);
        TLBytes result = BuildMutation(view, oldSet, packs, keywords, documents,
            oldSet.Count, out bool stored);
        return await CommitResultAsync(result, stored);
    }

    public async ValueTask<TLBytes> RemoveAsync(long userId, long documentId,
        long accessHash)
    {
        using TLStickerSetState? row = await FindOwnedSetByDocumentAsync(userId,
            documentId, accessHash);
        if (row is null) return StickerResults.Error("STICKER_ID_INVALID");
        var view = row.Value.AsStickerSetState();
        StickerSet oldSet = view.Get_SetView().AsStickerSet();
        Vector documents = RemoveDocument(view.Documents, documentId);
        Vector packs = RemovePackMetadata(view.Packs, documentId);
        Vector keywords = RemoveKeywordMetadata(view.Keywords, documentId);
        TLBytes result = BuildMutation(view, oldSet, packs, keywords, documents,
            Math.Max(0, oldSet.Count - 1), out bool stored);
        return await CommitResultAsync(result, stored);
    }

    public async ValueTask<TLBytes> MoveAsync(long userId, long documentId,
        long accessHash, int position)
    {
        using TLStickerSetState? row = await FindOwnedSetByDocumentAsync(userId,
            documentId, accessHash);
        if (row is null) return StickerResults.Error("STICKER_ID_INVALID");
        var view = row.Value.AsStickerSetState();
        StickerSet oldSet = view.Get_SetView().AsStickerSet();
        if (position < 0 || position >= oldSet.Count)
            return StickerResults.Error("STICKER_ID_INVALID");
        Vector documents = MoveDocument(view.Documents, documentId, position);
        TLBytes result = BuildMutation(view, oldSet,
            StickerVectors.CopyObjectVector(view.Packs), StickerVectors.CopyObjectVector(view.Keywords),
            documents, oldSet.Count, out bool stored);
        return await CommitResultAsync(result, stored);
    }

    public async ValueTask<TLBytes> ChangeAsync(long userId, long documentId,
        long accessHash, string? emoji, byte[]? maskCoords, string? keywordsText)
    {
        using TLStickerSetState? row = await FindOwnedSetByDocumentAsync(userId,
            documentId, accessHash);
        if (row is null) return StickerResults.Error("STICKER_ID_INVALID");
        var view = row.Value.AsStickerSetState();
        StickerSet oldSet = view.Get_SetView().AsStickerSet();
        if (!TryGetDocument(view.Documents, documentId, out TLDocument? owned))
            return StickerResults.Error("STICKER_ID_INVALID");
        using (owned)
        {
            string effectiveEmoji = emoji ?? ReadDocumentEmoji(
                owned!.Value.AsDocument());
            byte[]? effectiveMaskCoords = maskCoords ?? ReadDocumentMaskCoords(
                owned.Value.AsDocument());
            var item = new StickerItemInput(documentId, accessHash,
                effectiveEmoji, effectiveMaskCoords,
                keywordsText is null ? ReadKeywords(view.Keywords, documentId)
                    : StickerInput.SplitKeywords(keywordsText));
            if (!IsItemValid(StickerRows.Kind(oldSet), item)) return StickerResults.Error("STICKER_INVALID");
            using TLDocument decorated = Decorate(owned.Value.AsDocument(),
                view.SetId, oldSet.AccessHash, StickerRows.Kind(oldSet), oldSet.TextColor,
                item);
            Vector documents = ReplaceDocument(view.Documents, documentId,
                decorated.AsSpan());
            Vector packs = emoji is null ? StickerVectors.CopyObjectVector(view.Packs)
                : RemovePackMetadata(view.Packs, documentId);
            Vector keywords = keywordsText is null
                ? StickerVectors.CopyObjectVector(view.Keywords)
                : RemoveKeywordMetadata(view.Keywords, documentId);
            if (emoji is not null) AppendPack(ref packs, item);
            if (keywordsText is not null) AppendKeywords(ref keywords, item);
            TLBytes result = BuildMutation(view, oldSet, packs, keywords,
                documents, oldSet.Count, out bool stored);
            return await CommitResultAsync(result, stored);
        }
    }

    public async ValueTask<TLBytes> SetThumbAsync(long userId, long? setId,
        long? accessHash, string? shortName, long thumbId, long? thumbAccessHash)
    {
        using TLStickerSetState? row = await ResolveOwnedSetAsync(userId, setId,
            accessHash, shortName);
        if (row is null) return StickerResults.Error("STICKERSET_OWNER_ANONYMOUS");
        bool contained = ContainsDocument(row.Value.AsStickerSetState().Documents,
            thumbId);
        if (!contained)
        {
            if (!thumbAccessHash.HasValue) return StickerResults.Error("STICKER_ID_INVALID");
            using TLDocument? thumb = await _documents.GetDocumentAsync(thumbId,
                thumbAccessHash.Value);
            if (thumb is null) return StickerResults.Error("STICKER_ID_INVALID");
        }
        var view = row.Value.AsStickerSetState();
        StickerSet oldSet = view.Get_SetView().AsStickerSet();
        Vector packs = StickerVectors.CopyObjectVector(view.Packs);
        Vector keywords = StickerVectors.CopyObjectVector(view.Keywords);
        Vector documents = StickerVectors.CopyObjectVector(view.Documents);
        bool stored;
        TLBytes result;
        using (StickerSet updated = oldSet.Clone().ThumbDocumentId(thumbId)
                   .Hash(checked((int)(view.Revision + 1))).Build())
        {
            stored = PutSet(userId, view.SetId,
                Encoding.UTF8.GetString(view.ShortName), view.Revision + 1,
                updated, packs, keywords, documents);
            result = BuildFull(updated, packs, keywords, documents);
        }
        return await CommitResultAsync(result, stored);
    }

    public async ValueTask<TLBytes> RenameAsync(long userId, long? setId,
        long? accessHash, string? shortName, string title)
    {
        if (title.Length is < 1 or > 64) return StickerResults.Error("STICKERSET_INVALID");
        using TLStickerSetState? row = await ResolveOwnedSetAsync(userId, setId,
            accessHash, shortName);
        if (row is null) return StickerResults.Error("STICKERSET_OWNER_ANONYMOUS");
        var view = row.Value.AsStickerSetState();
        StickerSet oldSet = view.Get_SetView().AsStickerSet();
        Vector packs = StickerVectors.CopyObjectVector(view.Packs);
        Vector keywords = StickerVectors.CopyObjectVector(view.Keywords);
        Vector documents = StickerVectors.CopyObjectVector(view.Documents);
        bool stored;
        TLBytes result;
        using (StickerSet updated = oldSet.Clone()
                   .Title(Encoding.UTF8.GetBytes(title))
                   .Hash(checked((int)(view.Revision + 1))).Build())
        {
            stored = PutSet(userId, view.SetId,
                Encoding.UTF8.GetString(view.ShortName), view.Revision + 1,
                updated, packs, keywords, documents);
            result = BuildFull(updated, packs, keywords, documents);
        }
        return await CommitResultAsync(result, stored);
    }

    public async ValueTask<TLBytes> DeleteAsync(long userId, long? setId,
        long? accessHash, string? shortName)
    {
        using TLStickerSetState? row = await ResolveOwnedSetAsync(userId, setId,
            accessHash, shortName);
        if (row is null) return StickerResults.Error("STICKERSET_OWNER_ANONYMOUS");
        var view = row.Value.AsStickerSetState();
        long id = view.SetId;
        HashSet<long> documentIds = DocumentIds(view.Documents).ToHashSet();
        if (!await _accounts.PurgeSetAsync(id, documentIds))
        {
            return StickerResults.StorageError();
        }
        IReadOnlyCollection<TLChannelStickerState> channels =
            await _repository.GetChannelStatesAsync();
        try
        {
            foreach (TLChannelStickerState channel in channels)
            {
                var state = channel.AsChannelStickerState();
                if (state.StickerSetId != id && state.EmojiSetId != id) continue;
                long normal = state.StickerSetId == id ? 0 : state.StickerSetId;
                long emoji = state.EmojiSetId == id ? 0 : state.EmojiSetId;
                if (normal == 0 && emoji == 0)
                {
                    _repository.DeleteChannelState(state.ChannelId);
                }
                else
                {
                    using TLChannelStickerState updated = ChannelStickerState.Builder()
                        .ChannelId(state.ChannelId).StickerSetId(normal)
                        .EmojiSetId(emoji).Date(state.Date).Build();
                    if (!_repository.PutChannelState(updated)) return StickerResults.StorageError();
                }
            }
        }
        finally
        {
            foreach (TLChannelStickerState channel in channels) channel.Dispose();
        }
        if (!await _repository.DeleteSetAsync(id) ||
            !await _unitOfWork.SaveAsync()) return StickerResults.StorageError();
        return StickerResults.True();
    }

    private (TLBytes Result, bool Stored) BuildCreatedSet(long userId,
        long setId, long accessHash, string title, string shortName,
        StickerSetKind kind, bool textColor,
        IReadOnlyList<StickerItemInput> items,
        IReadOnlyList<TLDocument> sources, long? thumbId)
    {
        var documents = new Vector();
        var packs = new Vector();
        var keywords = new Vector();
        for (int i = 0; i < items.Count; i++)
        {
            using TLDocument decorated = Decorate(sources[i].AsDocument(),
                setId, accessHash, kind, textColor, items[i]);
            documents.AppendTLObject(decorated.AsSpan());
            AppendItemMetadata(ref packs, ref keywords, items[i]);
        }
        var setBuilder = StickerSet.Builder().Creator(true).Id(setId)
            .AccessHash(accessHash).Title(Encoding.UTF8.GetBytes(title))
            .ShortName(Encoding.UTF8.GetBytes(shortName)).Count(items.Count)
            .Hash(1);
        if (kind == StickerSetKind.Mask) setBuilder = setBuilder.Masks(true);
        if (kind == StickerSetKind.Emoji) setBuilder = setBuilder.Emojis(true);
        if (textColor) setBuilder = setBuilder.TextColor(true);
        if (thumbId.HasValue) setBuilder = setBuilder.ThumbDocumentId(thumbId.Value);
        using StickerSet set = setBuilder.Build();
        bool stored = PutSet(userId, setId, shortName, 1, set, packs,
            keywords, documents);
        return (BuildFull(set, packs, keywords, documents), stored);
    }

    private TLBytes BuildMutation(StickerSetState view,
        StickerSet oldSet, Vector packs, Vector keywords, Vector documents,
        int count, out bool stored)
    {
        long revision = view.Revision + 1;
        using StickerSet updated = oldSet.Clone().Count(count)
            .Hash(checked((int)revision)).Build();
        stored = PutSet(view.OwnerUserId, view.SetId,
            Encoding.UTF8.GetString(view.ShortName), revision, updated,
            packs, keywords, documents);
        return BuildFull(updated, packs, keywords, documents);
    }

    private bool PutSet(long ownerUserId, long setId,
        string shortName, long revision, StickerSet set, Vector packs,
        Vector keywords, Vector documents)
    {
        using TLStickerSetState row = StickerSetState.Builder()
            .OwnerUserId(ownerUserId).SetId(setId)
            .ShortName(Encoding.UTF8.GetBytes(shortName)).Revision(revision)
            .Set(set.ToReadOnlySpan()).Packs(packs).Keywords(keywords)
            .Documents(documents).Build();
        return _repository.PutSet(row);
    }

    private async ValueTask<TLBytes> CommitResultAsync(TLBytes result,
        bool stored)
    {
        if (stored && await _unitOfWork.SaveAsync()) return result;
        result.Dispose();
        return StickerResults.StorageError();
    }

    private static TLBytes BuildFull(StickerSet set, Vector packs,
        Vector keywords, Vector documents)
    {
        var result = MessagesStickerSet.Builder().Set(set.ToReadOnlySpan())
            .Packs(packs).Keywords(keywords).Documents(documents).Build();
        return result.TLBytes!.Value;
    }

    private async ValueTask<TLStickerSetState?> ResolveOwnedSetAsync(long userId,
        long? setId, long? accessHash, string? shortName)
    {
        TLStickerSetState? row = await _lookup.ResolveSetAsync(setId, accessHash,
            shortName);
        if (row is not null && row.Value.AsStickerSetState().OwnerUserId != userId)
        {
            row.Value.Dispose();
            return null;
        }
        return row;
    }

    private async ValueTask<TLStickerSetState?> FindOwnedSetByDocumentAsync(
        long userId, long documentId, long accessHash)
    {
        IReadOnlyCollection<TLStickerSetState> rows =
            await _repository.GetOwnedSetsAsync(userId);
        TLStickerSetState? found = null;
        foreach (TLStickerSetState row in rows)
        {
            if (found is null && TryGetDocument(row.AsStickerSetState().Documents,
                    documentId, out TLDocument? document))
            {
                using (document)
                {
                    if (document!.Value.AsDocument().AccessHash == accessHash)
                    {
                        found = row.AsStickerSetState().Clone().Build();
                    }
                }
            }
            row.Dispose();
        }
        return found;
    }

    private static TLDocument Decorate(Document source, long setId,
        long setAccessHash, StickerSetKind kind, bool textColor,
        StickerItemInput item)
    {
        Vector attributes = CopyNonStickerAttributes(source.Attributes);
        using TLInputStickerSet set = InputStickerSetID.Builder().Id(setId)
            .AccessHash(setAccessHash).Build();
        if (kind == StickerSetKind.Emoji)
        {
            using DocumentAttributeCustomEmoji attribute =
                DocumentAttributeCustomEmoji.Builder().Free(true)
                    .TextColor(textColor).Alt(Encoding.UTF8.GetBytes(item.Emoji))
                    .Stickerset(set.AsSpan()).Build();
            attributes.AppendTLObject(attribute.ToReadOnlySpan());
        }
        else
        {
            var builder = DocumentAttributeSticker.Builder()
                .Mask(kind == StickerSetKind.Mask)
                .Alt(Encoding.UTF8.GetBytes(item.Emoji)).Stickerset(set.AsSpan());
            if (item.MaskCoords is not null)
                builder = builder.MaskCoords(item.MaskCoords);
            using DocumentAttributeSticker attribute = builder.Build();
            attributes.AppendTLObject(attribute.ToReadOnlySpan());
        }
        return source.Clone().Attributes(attributes).Build();
    }

    private static Vector CopyNonStickerAttributes(Vector source)
    {
        var result = new Vector();
        int count = source.Count;
        for (int i = 0; i < count; i++)
        {
            Span<byte> bytes = source.ReadTLObject();
            var attribute = (DocumentAttributeView)bytes;
            if (!attribute.Is(out DocumentAttributeSticker _) &&
                !attribute.Is(out DocumentAttributeCustomEmoji _))
                result.AppendTLObject(bytes);
        }
        return result;
    }

    private static void AppendItemMetadata(ref Vector packs,
        ref Vector keywords, StickerItemInput item)
    {
        AppendPack(ref packs, item);
        AppendKeywords(ref keywords, item);
    }

    private static void AppendPack(ref Vector packs, StickerItemInput item)
    {
        var ids = new VectorOfLong(); ids.Append(item.DocumentId);
        using StickerPack pack = StickerPack.Builder()
            .Emoticon(Encoding.UTF8.GetBytes(item.Emoji)).Documents(ids).Build();
        packs.AppendTLObject(pack.ToReadOnlySpan());
    }

    private static void AppendKeywords(ref Vector keywords, StickerItemInput item)
    {
        if (item.Keywords.Length == 0) return;
        var values = new VectorOfString();
        foreach (string keyword in item.Keywords)
            values.AppendTLBytes(Encoding.UTF8.GetBytes(keyword));
        using StickerKeyword value = StickerKeyword.Builder()
            .DocumentId(item.DocumentId).Keyword(values).Build();
        keywords.AppendTLObject(value.ToReadOnlySpan());
    }

    private static Vector RemovePackMetadata(Vector source, long documentId)
    {
        var result = new Vector();
        int count = source.Count;
        for (int i = 0; i < count; i++)
        {
            Span<byte> bytes = source.ReadTLObject();
            var pack = (StickerPack)bytes;
            long[] ids = pack.Documents.ToArray().Where(id =>
                id != documentId).ToArray();
            if (ids.Length == 0) continue;
            using StickerPack updated = StickerPack.Builder()
                .Emoticon(pack.Emoticon).Documents(StickerVectors.ToLongVector(ids)).Build();
            result.AppendTLObject(updated.ToReadOnlySpan());
        }
        return result;
    }

    private static Vector RemoveKeywordMetadata(Vector source, long documentId)
    {
        var result = new Vector();
        int count = source.Count;
        for (int i = 0; i < count; i++)
        {
            Span<byte> bytes = source.ReadTLObject();
            var keyword = (StickerKeyword)bytes;
            if (keyword.DocumentId != documentId) result.AppendTLObject(bytes);
        }
        return result;
    }

    private static Vector RemoveDocument(Vector source, long documentId) =>
        ReplaceDocument(source, documentId, default);

    private static Vector ReplaceDocument(Vector source, long documentId,
        ReadOnlySpan<byte> replacement)
    {
        var result = new Vector();
        int count = source.Count;
        for (int i = 0; i < count; i++)
        {
            Span<byte> bytes = source.ReadTLObject();
            var document = (DocumentView)bytes;
            if (document.Is(out Document value) && value.Id == documentId)
            {
                if (!replacement.IsEmpty) result.AppendTLObject(replacement);
            }
            else result.AppendTLObject(bytes);
        }
        return result;
    }

    private static Vector MoveDocument(Vector source, long documentId,
        int position)
    {
        var values = new List<byte[]>();
        int count = source.Count;
        for (int i = 0; i < count; i++)
            values.Add(source.ReadTLObject().ToArray());
        int old = values.FindIndex(bytes =>
            ((DocumentView)bytes.AsSpan()).Is(out Document document) &&
            document.Id == documentId);
        byte[] moved = values[old];
        values.RemoveAt(old);
        values.Insert(position, moved);
        var result = new Vector();
        foreach (byte[] value in values) result.AppendTLObject(value);
        return result;
    }

    private static bool ContainsDocument(Vector source, long documentId) =>
        DocumentIds(source).Contains(documentId);

    private static long[] DocumentIds(Vector source)
    {
        var ids = new List<long>();
        int count = source.Count;
        for (int i = 0; i < count; i++)
        {
            var document = (DocumentView)source.ReadTLObject();
            if (document.Is(out Document value)) ids.Add(value.Id);
        }
        return ids.ToArray();
    }

    private static bool TryGetDocument(Vector source, long documentId,
        out TLDocument? result)
    {
        int count = source.Count;
        for (int i = 0; i < count; i++)
        {
            var document = (DocumentView)source.ReadTLObject();
            if (document.Is(out Document value) && value.Id == documentId)
            {
                result = value.Clone().Build();
                return true;
            }
        }
        result = null;
        return false;
    }

    private static string ReadDocumentEmoji(Document document)
    {
        Vector attributes = document.Attributes;
        int count = attributes.Count;
        for (int i = 0; i < count; i++)
        {
            var attribute = (DocumentAttributeView)attributes.ReadTLObject();
            if (attribute.Is(out DocumentAttributeSticker sticker))
                return Encoding.UTF8.GetString(sticker.Alt);
            if (attribute.Is(out DocumentAttributeCustomEmoji emoji))
                return Encoding.UTF8.GetString(emoji.Alt);
        }
        return string.Empty;
    }

    private static byte[]? ReadDocumentMaskCoords(Document document)
    {
        Vector attributes = document.Attributes;
        int count = attributes.Count;
        for (int i = 0; i < count; i++)
        {
            var attribute = (DocumentAttributeView)attributes.ReadTLObject();
            if (attribute.Is(out DocumentAttributeSticker sticker) &&
                sticker.Flags[0]) return sticker.MaskCoords.ToArray();
        }
        return null;
    }

    private static string[] ReadKeywords(Vector source, long documentId)
    {
        int count = source.Count;
        for (int i = 0; i < count; i++)
        {
            var keyword = (StickerKeyword)source.ReadTLObject();
            if (keyword.DocumentId != documentId) continue;
            VectorOfString values = keyword.Keyword;
            var result = new string[values.Count];
            for (int j = 0; j < result.Length; j++)
                result[j] = Encoding.UTF8.GetString(values.ReadTLBytes());
            return result;
        }
        return [];
    }

    private static bool IsShortNameValid(string shortName) =>
        ShortNamePattern.IsMatch(shortName);

    private static bool IsItemValid(StickerSetKind kind, StickerItemInput item) =>
        item.DocumentId != 0 && item.Emoji.Length > 0 &&
        (kind == StickerSetKind.Mask || item.MaskCoords is null);
}
