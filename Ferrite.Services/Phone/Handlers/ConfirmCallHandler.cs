// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Security.Cryptography;
using Ferrite.Crypto;
using Ferrite.Data.Repositories;
using Ferrite.Services.Calls;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.phone;
using Ferrite.Utils;
using PhoneResult = Ferrite.TL.baseLayer.phone.TLPhoneCall;

namespace Ferrite.Services.Phone.Handlers;

public sealed class ConfirmCallHandler : PhoneCallHandlerBase
{
    private const int DhValueLength = 256;

    private const long ReflectorConnectionId = 1;

    private readonly PrivacyEvaluator _privacy;
    private readonly ICallMediaRelay _relay;
    private readonly CallTurnConnectionBuilder _turn;
    private readonly UserSerializer _userSerializer;

    public ConfirmCallHandler(IUnitOfWork unitOfWork, IBlockedPeersRepository blockedPeersRepository, IAuthorizationRepository authorizationRepository, IUserRepository userRepository, UserSerializer userSerializer, ICallRegistry registry,
        IUpdatesService updates, IMTProtoTime time, PrivacyEvaluator privacy,
        ICallMediaRelay relay, CallTurnConnectionBuilder turn)
        : base(unitOfWork, blockedPeersRepository, authorizationRepository, userRepository, registry, updates, time)
    {
        _privacy = privacy;
        _relay = relay;
        _turn = turn;
        _userSerializer = userSerializer;
    }

    [TLFunction(Constructors.baseLayer_ConfirmCall)]
    public async ValueTask<PhoneResult> Handle(long authKeyId, TLBytes q)
    {
        var request = (ConfirmCall)q;
        (long callId, long accessHash) = ReadInputPhoneCall(request.Peer);
        byte[] gA = request.GA.ToArray();
        long keyFingerprint = request.KeyFingerprint;

        long? callerUserIdValue = await GetCurrentUserIdAsync(authKeyId);
        if (callerUserIdValue is not long callerUserId)
        {
            return Error(400, "AUTH_KEY_INVALID"u8);
        }

        if (gA.Length != DhValueLength ||
            !TelegramDhParameters.IsValidSecretChatPublicValue(gA))
        {
            return Error(400, "GA_INVALID"u8);
        }

        CallSnapshot? existing = Registry.Get(callId);
        if (existing is null || existing.AccessHash != accessHash)
        {
            return Error(400, "CALL_PEER_INVALID"u8);
        }

        if (existing.CallerUserId != callerUserId ||
            existing.CallerAuthKeyId != authKeyId)
        {
            return Error(400, "CALL_PEER_INVALID"u8);
        }

        if (!CommitmentMatches(gA, existing.GAHash))
        {
            return Error(400, "GA_INVALID"u8);
        }

        switch (existing.State)
        {
            case CallSessionState.Confirmed:
                return BuildResult(callerUserId, BuildFinalCall(existing), existing.CallerUserId,
                    existing.CalleeUserId, _userSerializer);
            case CallSessionState.Discarded:
                return Error(400, "CALL_ALREADY_DECLINED"u8);
            case CallSessionState.Requested:
            case CallSessionState.Received:
                return Error(400, "CALL_PEER_INVALID"u8);
        }

        bool p2pAllowed = (existing.NegotiatedProtocol?.UdpP2p ?? false) &&
                          await _privacy.IsPhoneP2PAllowedBilateral(
                              existing.CallerUserId, existing.CalleeUserId);

        CallRelayAllocation? allocation = _relay.CreateAllocation(callId);
        if (allocation is null)
        {
            return Error(500, "CALL_OCCUPY_FAILED"u8);
        }

        var connections = new List<byte[]>();
        if (_relay.AdvertisedEndpoint is { } endpoint)
        {
            connections.Add(BuildReflectorConnection(ReflectorConnectionId,
                endpoint.Address.ToString(), endpoint.Port, allocation.PeerTag));
        }

        connections.AddRange(_turn.BuildConnections(callId));

        int startDate = Now();
        CallRegistryResult result = Registry.TryConfirm(callId, accessHash, authKeyId,
            gA, keyFingerprint, p2pAllowed, connections, allocation.Prefix.ToArray(),
            startDate);
        switch (result.Status)
        {
            case CallRegistryStatus.Ok:
                break;
            case CallRegistryStatus.Duplicate:
                return BuildResult(callerUserId, BuildFinalCall(result.Call!),
                    result.Call!.CallerUserId, result.Call.CalleeUserId,
                    _userSerializer);
            case CallRegistryStatus.AlreadyDiscarded:
            case CallRegistryStatus.InvalidState:
                _relay.RemoveAllocation(callId);
                return Error(400, "CALL_ALREADY_DECLINED"u8);
            default:
                _relay.RemoveAllocation(callId);
                return Error(400, "CALL_PEER_INVALID"u8);
        }

        CallSnapshot call = result.Call!;
        byte[] finalCall = BuildFinalCall(call);
        if (call.CalleeAuthKeyId is long calleeAuthKey)
        {
            await PushCallUpdate(call.CalleeUserId, finalCall,
                UpdateDeliveryScope.ForAuthKey(calleeAuthKey));
        }

        return BuildResult(callerUserId, finalCall, call.CallerUserId, call.CalleeUserId,
            _userSerializer);
    }

    private static bool CommitmentMatches(byte[] gA, byte[] gAHash)
    {
        if (gAHash.Length != SHA256.HashSizeInBytes)
        {
            return false;
        }

        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        SHA256.HashData(gA, digest);
        return CryptographicOperations.FixedTimeEquals(digest, gAHash);
    }
}
