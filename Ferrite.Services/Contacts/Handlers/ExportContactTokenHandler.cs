// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;

namespace Ferrite.Services.Handlers.ContactMethods;

public sealed class ExportContactTokenHandler
{
    private readonly ProfileStore _profiles;

    public ExportContactTokenHandler(ProfileStore profiles) =>
        _profiles = profiles;

    [TLFunction(Constructors.baseLayer_ExportContactToken)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await _profiles.GetUserIdAsync(authKeyId);
        return userId.HasValue
            ? await _profiles.ExportContactTokenAsync(userId.Value)
            : RpcErrorGenerator.GenerateError(401, "AUTH_KEY_INVALID"u8);
    }
}
