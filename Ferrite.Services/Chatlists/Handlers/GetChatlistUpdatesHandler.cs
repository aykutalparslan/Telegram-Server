// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.Services.Chatlists;
using Ferrite.TL;
using Ferrite.TL.baseLayer.chatlists;

namespace Ferrite.Services.Handlers.ChatlistMethods;

public sealed class GetChatlistUpdatesHandler : ChatlistHandlerBase
{
    private readonly ChatlistImportStore _imports;

    public GetChatlistUpdatesHandler(IUnitOfWork unitOfWork,
        IAuthorizationRepository authorizationRepository,
        ChatlistImportStore store) : base(unitOfWork, authorizationRepository)
    {
        _imports = store;
    }

    [TLFunction(Constructors.baseLayer_GetChatlistUpdates)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await GetUserIdAsync(authKeyId);
        if (userId == null) return AuthError();
        var request = (GetChatlistUpdates)q;
        if (!TryReadFilterId(request.Get_ChatlistView(), out int filterId))
        {
            return RequestError();
        }
        return await _imports.GetUpdatesAsync(userId.Value, filterId);
    }
}
