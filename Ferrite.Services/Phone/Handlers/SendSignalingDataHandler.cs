// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.Services.Calls;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.phone;
using Ferrite.Utils;

namespace Ferrite.Services.Phone.Handlers;

public sealed class SendSignalingDataHandler : PhoneCallHandlerBase
{
    private readonly CallSignalingLimiter _limiter;

    public SendSignalingDataHandler(IUnitOfWork unitOfWork, IBlockedPeersRepository blockedPeersRepository, IAuthorizationRepository authorizationRepository, IUserRepository userRepository, ICallRegistry registry,
        IUpdatesService updates, IMTProtoTime time, CallSignalingLimiter limiter)
        : base(unitOfWork, blockedPeersRepository, authorizationRepository, userRepository, registry, updates, time)
    {
        _limiter = limiter;
    }

    [TLFunction(Constructors.baseLayer_SendSignalingData)]
    public async ValueTask<TLBool> Handle(long authKeyId, TLBytes q)
    {
        var request = (SendSignalingData)q;
        (long callId, long accessHash) = ReadInputPhoneCall(request.Peer);
        byte[] data = request.Data.ToArray();

        long? senderUserIdValue = await GetCurrentUserIdAsync(authKeyId);
        if (senderUserIdValue is not long senderUserId)
        {
            return (TLBool)RpcErrorGenerator.GenerateError(400, "AUTH_KEY_INVALID"u8);
        }

        CallSnapshot? snapshot = Registry.Get(callId);
        if (snapshot is null || snapshot.AccessHash != accessHash)
        {
            return (TLBool)RpcErrorGenerator.GenerateError(400, "CALL_PEER_INVALID"u8);
        }

        if (snapshot.State != CallSessionState.Confirmed ||
            snapshot.CalleeAuthKeyId is not long calleeAuthKey)
        {
            return (TLBool)RpcErrorGenerator.GenerateError(400,
                "CALL_PEER_INVALID"u8);
        }

        long targetUserId;
        long targetAuthKey;
        if (senderUserId == snapshot.CallerUserId && authKeyId == snapshot.CallerAuthKeyId)
        {
            targetUserId = snapshot.CalleeUserId;
            targetAuthKey = calleeAuthKey;
        }
        else if (senderUserId == snapshot.CalleeUserId && authKeyId == calleeAuthKey)
        {
            targetUserId = snapshot.CallerUserId;
            targetAuthKey = snapshot.CallerAuthKeyId;
        }
        else
        {
            return (TLBool)RpcErrorGenerator.GenerateError(400, "CALL_PEER_INVALID"u8);
        }

        SignalingDecision decision = _limiter.Evaluate(callId, data.Length);
        if (decision != SignalingDecision.Forward)
        {
            return (TLBool)RpcErrorGenerator.GenerateError(400, "DATA_INVALID"u8);
        }

        TLUpdate update = UpdatePhoneCallSignalingData.Builder()
            .PhoneCallId(callId)
            .Data(data)
            .Build();
        await Updates.EnqueueUpdate(targetUserId, update,
            UpdateDeliveryScope.ForAuthKey(targetAuthKey));
        return BoolTrue.Builder().Build();
    }
}
