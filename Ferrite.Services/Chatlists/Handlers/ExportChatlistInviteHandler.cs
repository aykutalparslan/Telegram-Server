// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer.chatlists;

namespace Ferrite.Services.Handlers.ChatlistMethods;

public sealed class ExportChatlistInviteHandler : ChatlistHandlerBase
{
    public ExportChatlistInviteHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository,
        ChatlistInviteStore invites) : base(unitOfWork, authorizationRepository, invites)
    {
    }

    [TLFunction(Constructors.baseLayer_ExportChatlistInvite)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await GetUserIdAsync(authKeyId);
        if (userId == null) return AuthError();

        var request = (ExportChatlistInvite)q;
        if (!TryReadFilterId(request.Get_ChatlistView(), out int filterId) ||
            !TryReadPeers(request.Peers, userId.Value, out DialogPeerKey[] peers))
        {
            return RequestError();
        }
        byte[] title = request.Title.ToArray();
        return await Invites.ExportAsync(userId.Value, filterId, title, peers);
    }
}
