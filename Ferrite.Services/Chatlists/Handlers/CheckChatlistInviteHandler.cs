// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.Services.Chatlists;
using Ferrite.TL;
using Ferrite.TL.baseLayer.chatlists;

namespace Ferrite.Services.Handlers.ChatlistMethods;

public sealed class CheckChatlistInviteHandler : ChatlistHandlerBase
{
    private readonly ChatlistImportStore _imports;

    public CheckChatlistInviteHandler(IUnitOfWork unitOfWork,
        IAuthorizationRepository authorizationRepository,
        ChatlistImportStore store) : base(unitOfWork, authorizationRepository)
    {
        _imports = store;
    }

    [TLFunction(Constructors.baseLayer_CheckChatlistInvite)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await GetUserIdAsync(authKeyId);
        if (userId == null) return AuthError();
        string slug = Encoding.UTF8.GetString(((CheckChatlistInvite)q).Slug);
        return await _imports.CheckAsync(userId.Value, slug);
    }
}
