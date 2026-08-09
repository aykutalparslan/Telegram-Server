// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public class DocumentsRepository : IDocumentsRepository
{
    private readonly IKVStore _store;
    private readonly IKVStore _storeBySha256;

    public DocumentsRepository(IKVStore store, IKVStore storeBySha256)
    {
        _store = store;
        _store.SetSchema(new TableDefinition("ferrite", "documents",
            new KeyDefinition("pk",
                new DataColumn { Name = "document_id", Type = DataType.Long })));
        _storeBySha256 = storeBySha256;
        _storeBySha256.SetSchema(new TableDefinition("ferrite", "documents_by_sha256_tl1",
            new KeyDefinition("pk",
                new DataColumn { Name = "sha256", Type = DataType.Bytes })));
    }

    public bool PutDocument(TLBytes document, byte[] sha256)
    {
        long documentId = ((Document)document.AsSpan()).Id;
        bool stored = _store.Put(document.AsSpan().ToArray(), documentId);
        using var reference = DocumentReference.Builder().DocumentId(documentId).Build();
        return _storeBySha256.Put(reference.ToReadOnlySpan().ToArray(), sha256) && stored;
    }

    public TLBytes? GetDocument(long documentId)
    {
        var documentBytes = _store.Get(documentId);
        if (documentBytes == null) return null;
        return new TLBytes(documentBytes, 0, documentBytes.Length);
    }

    public TLBytes? GetDocumentBySha256(byte[] sha256)
    {
        var documentIdBytes = _storeBySha256.Get(sha256);
        if (documentIdBytes is not { Length: > 0 }) return null;
        var value = new TLBytes(documentIdBytes, 0, documentIdBytes.Length);
        if (value.Constructor != Constructors.baseLayer_DocumentReference)
            throw new InvalidDataException("Document reference codec/version mismatch.");
        return GetDocument(((TLDocumentReference)value).AsDocumentReference().DocumentId);
    }

    public bool DeleteDocument(long documentId)
    {
        return _store.Delete(documentId);
    }
}
