// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.Services.Chatlists;
using Ferrite.TL;
using Ferrite.TL.baseLayer.chatlists;

namespace Ferrite.Services.Handlers.ChatlistMethods;

public sealed class JoinChatlistInviteHandler : ChatlistHandlerBase
{
    private readonly ChatlistImportStore _imports;

    public JoinChatlistInviteHandler(IUnitOfWork unitOfWork,
        IAuthorizationRepository authorizationRepository,
        ChatlistImportStore store) : base(unitOfWork, authorizationRepository)
    {
        _imports = store;
    }

    [TLFunction(Constructors.baseLayer_JoinChatlistInvite)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await GetUserIdAsync(authKeyId);
        if (userId == null) return AuthError();
        var request = (JoinChatlistInvite)q;
        if (!TryReadPeers(request.Peers, userId.Value,
                out DialogPeerKey[] peers))
        {
            return RequestError();
        }
        string slug = Encoding.UTF8.GetString(request.Slug);
        return await _imports.JoinInviteAsync(authKeyId, userId.Value, slug, peers);
    }
}
