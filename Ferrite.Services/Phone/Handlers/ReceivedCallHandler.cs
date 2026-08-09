// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.Services.Calls;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.phone;
using Ferrite.Utils;

namespace Ferrite.Services.Phone.Handlers;

public sealed class ReceivedCallHandler : PhoneCallHandlerBase
{
    public ReceivedCallHandler(IUnitOfWork unitOfWork, IBlockedPeersRepository blockedPeersRepository, IAuthorizationRepository authorizationRepository, IUserRepository userRepository, ICallRegistry registry,
        IUpdatesService updates, IMTProtoTime time)
        : base(unitOfWork, blockedPeersRepository, authorizationRepository, userRepository, registry, updates, time)
    {
    }

    [TLFunction(Constructors.baseLayer_ReceivedCall)]
    public async ValueTask<TLBool> Handle(long authKeyId, TLBytes q)
    {
        var request = (ReceivedCall)q;
        (long callId, long accessHash) = ReadInputPhoneCall(request.Peer);

        long? calleeUserIdValue = await GetCurrentUserIdAsync(authKeyId);
        if (calleeUserIdValue is not long calleeUserId)
        {
            return (TLBool)RpcErrorGenerator.GenerateError(400, "AUTH_KEY_INVALID"u8);
        }

        int date = Now();
        CallRegistryResult result = Registry.TryMarkReceived(callId, accessHash,
            calleeUserId, date);
        switch (result.Status)
        {
            case CallRegistryStatus.Ok:
                break;
            case CallRegistryStatus.NotFound:
            case CallRegistryStatus.AccessHashInvalid:
                return (TLBool)RpcErrorGenerator.GenerateError(400,
                    "CALL_PEER_INVALID"u8);
            case CallRegistryStatus.WrongUser:
                return (TLBool)RpcErrorGenerator.GenerateError(400,
                    "CALL_PEER_INVALID"u8);
            case CallRegistryStatus.AlreadyDiscarded:
                return (TLBool)RpcErrorGenerator.GenerateError(400,
                    "CALL_ALREADY_DECLINED"u8);
            default:
                return (TLBool)RpcErrorGenerator.GenerateError(400,
                    "CALL_PEER_INVALID"u8);
        }

        CallSnapshot call = result.Call!;
        // Notify the initiating caller device that the call was received so it
        // can start its own ring timeout.
        await PushCallUpdate(call.CallerUserId, BuildWaiting(call),
            UpdateDeliveryScope.ForAuthKey(call.CallerAuthKeyId));
        return BoolTrue.Builder().Build();
    }
}
