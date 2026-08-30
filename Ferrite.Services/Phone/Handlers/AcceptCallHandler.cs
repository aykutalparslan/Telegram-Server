// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Crypto;
using Ferrite.Data.Repositories;
using Ferrite.Services.Calls;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.phone;
using Ferrite.Utils;
using PhoneResult = Ferrite.TL.baseLayer.phone.TLPhoneCall;

namespace Ferrite.Services.Phone.Handlers;

public sealed class AcceptCallHandler : PhoneCallHandlerBase
{
    private const int DhValueLength = 256;

    private readonly CallRegistryOptions _options;
    private readonly UserSerializer _userSerializer;

    public AcceptCallHandler(IUnitOfWork unitOfWork, IBlockedPeersRepository blockedPeersRepository, IAuthorizationRepository authorizationRepository, IUserRepository userRepository, UserSerializer userSerializer, ICallRegistry registry,
        IUpdatesService updates, IMTProtoTime time, CallRegistryOptions options)
        : base(unitOfWork, blockedPeersRepository, authorizationRepository, userRepository, registry, updates, time)
    {
        _options = options;
        _userSerializer = userSerializer;
    }

    [TLFunction(Constructors.baseLayer_AcceptCall)]
    public async ValueTask<PhoneResult> Handle(long authKeyId, TLBytes q)
    {
        var request = (AcceptCall)q;
        (long callId, long accessHash) = ReadInputPhoneCall(request.Peer);
        byte[] gB = request.GB.ToArray();
        CallProtocol calleeProtocol = ReadProtocol(request.Protocol);

        long? calleeUserIdValue = await GetCurrentUserIdAsync(authKeyId);
        if (calleeUserIdValue is not long calleeUserId)
        {
            return Error(400, "AUTH_KEY_INVALID"u8);
        }

        if (gB.Length != DhValueLength ||
            !TelegramDhParameters.IsValidSecretChatPublicValue(gB))
        {
            return Error(400, "GB_INVALID"u8);
        }

        CallSnapshot? existing = Registry.Get(callId);
        if (existing is null || existing.AccessHash != accessHash)
        {
            return Error(400, "CALL_PEER_INVALID"u8);
        }

        if (existing.CalleeUserId != calleeUserId)
        {
            return Error(400, "CALL_PEER_INVALID"u8);
        }

        var (negotiated, protocolError) = CallProtocolNegotiator.Negotiate(
            existing.CallerProtocol, calleeProtocol, _options);
        if (negotiated is null)
        {
            return protocolError switch
            {
                CallProtocolError.LayerInvalid =>
                    Error(400, "CALL_PROTOCOL_LAYER_INVALID"u8),
                CallProtocolError.FlagsInvalid =>
                    Error(400, "CALL_PROTOCOL_FLAGS_INVALID"u8),
                _ => Error(400, "PARTICIPANT_VERSION_OUTDATED"u8),
            };
        }

        int date = Now();
        CallRegistryResult result = Registry.TryAccept(callId, accessHash,
            calleeUserId, authKeyId, gB, calleeProtocol, negotiated, date);
        switch (result.Status)
        {
            case CallRegistryStatus.Ok:
                break;
            case CallRegistryStatus.NotFound:
            case CallRegistryStatus.AccessHashInvalid:
            case CallRegistryStatus.WrongUser:
                return Error(400, "CALL_PEER_INVALID"u8);
            case CallRegistryStatus.AlreadyAccepted:
                return Error(400, "CALL_ALREADY_ACCEPTED"u8);
            case CallRegistryStatus.AlreadyDiscarded:
                return Error(400, "CALL_ALREADY_DECLINED"u8);
            default:
                return Error(400, "CALL_PEER_INVALID"u8);
        }

        CallSnapshot call = result.Call!;
        await PushCallUpdate(call.CallerUserId, BuildAccepted(call),
            UpdateDeliveryScope.ForAuthKey(call.CallerAuthKeyId));
        await PushCallUpdate(call.CalleeUserId,
            BuildDiscarded(call.CallId,
                Constructors.baseLayer_PhoneCallDiscardReasonBusy, call.Video, 0),
            UpdateDeliveryScope.ExcludingAuthKeys(new[] { authKeyId }));
        return BuildResult(calleeUserId, BuildWaiting(call), call.CallerUserId,
            call.CalleeUserId, _userSerializer);
    }
}
