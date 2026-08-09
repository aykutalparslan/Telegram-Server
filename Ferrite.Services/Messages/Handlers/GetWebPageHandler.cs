// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class GetWebPageHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IWebPagesRepository _webPagesRepository;

    private readonly IUnitOfWork _unitOfWork;

    public GetWebPageHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IWebPagesRepository webPagesRepository)
    {
        _authorizationRepository = authorizationRepository;
        _webPagesRepository = webPagesRepository;

        _unitOfWork = unitOfWork;
    }

    [TLFunction(Constructors.baseLayer_GetWebPage)]
    public async Task<Ferrite.TL.baseLayer.messages.TLWebPage> Handle(
        long authKeyId, TLBytes q)
    {
        using var auth = await _authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        if (auth == null)
        {
            return (Ferrite.TL.baseLayer.messages.TLWebPage)
                RpcErrorGenerator.GenerateError(400, "AUTH_KEY_INVALID"u8);
        }

        var request = (GetWebPage)q;
        string url = Encoding.UTF8.GetString(request.Url);
        int requestedHash = request.Hash;

        using TLWebPageInfo? cached = await _webPagesRepository
            .GetWebPageAsync(url);
        byte[] webPageBytes;
        if (cached == null)
        {
            using Ferrite.TL.baseLayer.TLWebPage empty = WebPageEmpty.Builder()
                .Id(0)
                .Url(Encoding.UTF8.GetBytes(url))
                .Build();
            webPageBytes = empty.AsSpan().ToArray();
        }
        else
        {
            var info = cached.Value.AsWebPageInfo();
            if (requestedHash != 0 && requestedHash == info.Hash)
            {
                using Ferrite.TL.baseLayer.TLWebPage notModified =
                    WebPageNotModified.Builder().Build();
                webPageBytes = notModified.AsSpan().ToArray();
            }
            else
            {
                webPageBytes = info.Webpage.ToArray();
            }
        }

        return MessagesWebPage.Builder()
            .Webpage(webPageBytes)
            .Chats(new Vector())
            .Users(new Vector())
            .Build();
    }
}
