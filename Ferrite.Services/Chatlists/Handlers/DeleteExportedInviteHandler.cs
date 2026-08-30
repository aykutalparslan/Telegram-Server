// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.Services.Chatlists;
using Ferrite.TL;
using Ferrite.TL.baseLayer.chatlists;

namespace Ferrite.Services.Handlers.ChatlistMethods;

public sealed class DeleteExportedInviteHandler : ChatlistHandlerBase
{
    private readonly ChatlistInviteStore _invites;

    public DeleteExportedInviteHandler(IUnitOfWork unitOfWork,
        IAuthorizationRepository authorizationRepository,
        ChatlistInviteStore store) : base(unitOfWork, authorizationRepository)
    {
        _invites = store;
    }

    [TLFunction(Constructors.baseLayer_DeleteExportedInvite)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await GetUserIdAsync(authKeyId);
        if (userId == null) return AuthError();

        var request = (DeleteExportedInvite)q;
        if (!TryReadFilterId(request.Get_ChatlistView(), out int filterId))
        {
            return RequestError();
        }
        string slug = ReadSlug(request.Slug);
        return await _invites.DeleteAsync(userId.Value, filterId, slug);
    }
}
