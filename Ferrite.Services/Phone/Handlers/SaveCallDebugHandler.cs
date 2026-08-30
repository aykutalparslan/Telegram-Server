// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.Services.Calls;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.phone;
using Ferrite.Utils;

namespace Ferrite.Services.Phone.Handlers;

public sealed class SaveCallDebugHandler : PhoneCallHandlerBase
{
    private const int MaxDebugLength = 64 * 1024;

    public SaveCallDebugHandler(IUnitOfWork unitOfWork, IBlockedPeersRepository blockedPeersRepository, IAuthorizationRepository authorizationRepository, IUserRepository userRepository, ICallRegistry registry,
        IUpdatesService updates, IMTProtoTime time)
        : base(unitOfWork, blockedPeersRepository, authorizationRepository, userRepository, registry, updates, time)
    {
    }

    [TLFunction(Constructors.baseLayer_SaveCallDebug)]
    public async ValueTask<TLBool> Handle(long authKeyId, TLBytes q)
    {
        var request = (SaveCallDebug)q;
        (long callId, long accessHash) = ReadInputPhoneCall(request.Peer);
        int debugLength = ((DataJSON)request.Debug).Data.Length;

        long? userIdValue = await GetCurrentUserIdAsync(authKeyId);
        if (userIdValue is not long userId)
        {
            return (TLBool)RpcErrorGenerator.GenerateError(400, "AUTH_KEY_INVALID"u8);
        }

        if (debugLength > MaxDebugLength)
        {
            return (TLBool)RpcErrorGenerator.GenerateError(400, "DATA_JSON_INVALID"u8);
        }

        if (ResolveParticipantCall(callId, accessHash, userId) is null)
        {
            return (TLBool)RpcErrorGenerator.GenerateError(400, "CALL_PEER_INVALID"u8);
        }

        return BoolTrue.Builder().Build();
    }
}
