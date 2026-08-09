// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer.account;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class InitTakeoutSessionHandler : AccountSettingsHandlerBase
{
    public InitTakeoutSessionHandler(AccountSettingsStore store) : base(store) { }

    [TLFunction(Constructors.baseLayer_InitTakeoutSession)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await GetUserIdAsync(authKeyId);
        if (!userId.HasValue) return AuthError();
        var request = new InitTakeoutSession(q.AsSpan());
        if (request.Files && request.FileMaxSize < 0)
            return Invalid("FILE_MAX_SIZE_INVALID");
        return await Store.InitTakeoutAsync(authKeyId, userId.Value,
            request.Contacts, request.MessageUsers, request.MessageChats,
            request.MessageMegagroups, request.MessageChannels, request.Files,
            request.FileMaxSize);
    }
}

