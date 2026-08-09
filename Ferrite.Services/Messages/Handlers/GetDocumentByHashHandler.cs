// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class GetDocumentByHashHandler
{
    private readonly IDocumentsRepository _documentsRepository;

    private readonly IUnitOfWork _unitOfWork;

    public GetDocumentByHashHandler(IUnitOfWork unitOfWork, IDocumentsRepository documentsRepository)
    {
        _documentsRepository = documentsRepository;

        _unitOfWork = unitOfWork;
    }

    [TLFunction(Constructors.baseLayer_GetDocumentByHash)]
    public ValueTask<TLDocument> Handle(long authKeyId, TLBytes q)
    {
        var request = (GetDocumentByHash)q;
        byte[] sha256 = request.Sha256.ToArray();
        long size = request.Size;
        byte[] mimeType = request.MimeType.ToArray();

        TLBytes? stored = _documentsRepository.GetDocumentBySha256(sha256);
        if (stored != null)
        {
            var document = (Document)stored.Value.AsSpan();
            if (document.Constructor == Constructors.baseLayer_Document &&
                document.Size == size && document.MimeType.SequenceEqual(mimeType))
            {
                return ValueTask.FromResult((TLDocument)stored.Value);
            }
            stored.Value.Dispose();
        }

        return ValueTask.FromResult<TLDocument>(DocumentEmpty.Builder().Id(0).Build());
    }
}
