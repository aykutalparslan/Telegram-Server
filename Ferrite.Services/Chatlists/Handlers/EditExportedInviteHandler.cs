// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer.chatlists;

namespace Ferrite.Services.Handlers.ChatlistMethods;

public sealed class EditExportedInviteHandler : ChatlistHandlerBase
{
    public EditExportedInviteHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository,
        ChatlistInviteStore invites) : base(unitOfWork, authorizationRepository, invites)
    {
    }

    [TLFunction(Constructors.baseLayer_EditExportedInvite)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await GetUserIdAsync(authKeyId);
        if (userId == null) return AuthError();

        var request = (EditExportedInvite)q;
        if (!TryReadFilterId(request.Get_ChatlistView(), out int filterId))
        {
            return RequestError();
        }
        DialogPeerKey[]? peers = null;
        if (request.Flags[2] &&
            !TryReadPeers(request.Peers, userId.Value, out peers))
        {
            return RequestError();
        }
        string slug = ReadSlug(request.Slug);
        byte[]? title = request.Flags[1] ? request.Title.ToArray() : null;
        return await Invites.EditAsync(userId.Value, filterId, slug, title, peers);
    }
}
