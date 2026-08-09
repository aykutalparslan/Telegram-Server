// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.users;

namespace Ferrite.Services.Handlers.UserMethods;

public sealed class GetRequirementsToContactHandler
{
    private readonly ProfileStore _profiles;

    public GetRequirementsToContactHandler(ProfileStore profiles) =>
        _profiles = profiles;

    [TLFunction(Constructors.baseLayer_GetRequirementsToContact)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await _profiles.GetUserIdAsync(authKeyId);
        if (!userId.HasValue)
            return RpcErrorGenerator.GenerateError(401, "AUTH_KEY_INVALID"u8);
        Vector vector = new GetRequirementsToContact(q.AsSpan()).Id;
        var ids = new List<long>(vector.Count);
        for (int i = 0; i < vector.Count; i++)
        {
            InputUserView input = vector.ReadTLObject();
            if (input.Is(out InputUser user)) ids.Add(user.UserId);
            else if (input.Is(out InputUserFromMessage fromMessage))
                ids.Add(fromMessage.UserId);
            else if (input.Is(out InputUserSelf _)) ids.Add(userId.Value);
            else return RpcErrorGenerator.GenerateError(400,
                "USER_ID_INVALID"u8);
        }
        return _profiles.GetRequirementsToContact(userId.Value, ids);
    }
}
