// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL;
using Ferrite.TL.baseLayer.contacts;

namespace Ferrite.Services.Handlers.ContactMethods;

public sealed class ImportContactTokenHandler
{
    private readonly ProfileStore _profiles;

    public ImportContactTokenHandler(ProfileStore profiles) =>
        _profiles = profiles;

    [TLFunction(Constructors.baseLayer_ImportContactToken)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await _profiles.GetUserIdAsync(authKeyId);
        if (!userId.HasValue)
            return RpcErrorGenerator.GenerateError(401, "AUTH_KEY_INVALID"u8);
        string token = Encoding.UTF8.GetString(
            new ImportContactToken(q.AsSpan()).Token);
        return await _profiles.ImportContactTokenAsync(userId.Value, token);
    }
}
