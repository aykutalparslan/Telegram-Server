// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;

namespace Ferrite.Data.Repositories;

public interface IDocumentsRepository
{
    public bool PutDocument(TLBytes document, byte[] sha256);
    public TLBytes? GetDocument(long documentId);
    public TLBytes? GetDocumentBySha256(byte[] sha256);
    public bool DeleteDocument(long documentId);
}
