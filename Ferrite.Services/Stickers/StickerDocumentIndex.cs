// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Stickers;

public sealed class StickerDocumentIndex
{
    private readonly IStickerRepository _repository;
    private readonly IDocumentsRepository _documentsRepository;

    public StickerDocumentIndex(IStickerRepository repository,
        IDocumentsRepository documentsRepository)
    {
        _repository = repository;
        _documentsRepository = documentsRepository;
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
            StickerRows.Dispose(rows);
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

    public async ValueTask<TLDocument?> FindStickerDocumentAsync(long id,
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
                    StickerRows.Kind(set.Get_SetView().AsStickerSet()) !=
                    requiredKind.Value)
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
            StickerRows.Dispose(rows);
        }
    }

    public Vector BuildDocuments(IEnumerable<long> ids,
        IReadOnlyCollection<TLStickerSetState> rows, bool includeGeneral = false,
        StickerSetKind? requiredKind = null)
    {
        var result = new Vector();
        foreach (long id in ids)
        {
            bool found = false;
            foreach (TLStickerSetState row in rows)
            {
                if (requiredKind.HasValue && StickerRows.Kind(row
                        .AsStickerSetState().Get_SetView().AsStickerSet()) !=
                    requiredKind.Value)
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
}
