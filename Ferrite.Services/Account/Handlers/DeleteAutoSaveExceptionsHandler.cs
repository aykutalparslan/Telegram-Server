// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class DeleteAutoSaveExceptionsHandler : AccountSettingsHandlerBase
{
    public DeleteAutoSaveExceptionsHandler(AccountSettingsStore store) : base(store) { }

    [TLFunction(Constructors.baseLayer_DeleteAutoSaveExceptions)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await GetUserIdAsync(authKeyId);
        return userId.HasValue
            ? await Store.DeleteAutoSaveExceptionsAsync(userId.Value)
            : AuthError();
    }
}

