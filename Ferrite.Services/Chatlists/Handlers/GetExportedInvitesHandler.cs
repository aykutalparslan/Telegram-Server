// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.Services.Chatlists;
using Ferrite.TL;
using Ferrite.TL.baseLayer.chatlists;

namespace Ferrite.Services.Handlers.ChatlistMethods;

public sealed class GetExportedInvitesHandler : ChatlistHandlerBase
{
    private readonly ChatlistInviteStore _invites;

    public GetExportedInvitesHandler(IUnitOfWork unitOfWork,
        IAuthorizationRepository authorizationRepository,
        ChatlistInviteStore store) : base(unitOfWork, authorizationRepository)
    {
        _invites = store;
    }

    [TLFunction(Constructors.baseLayer_GetExportedInvites)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await GetUserIdAsync(authKeyId);
        if (userId == null) return AuthError();

        var request = (GetExportedInvites)q;
        if (!TryReadFilterId(request.Get_ChatlistView(), out int filterId))
        {
            return RequestError();
        }
        return await _invites.GetAsync(userId.Value, filterId);
    }
}
