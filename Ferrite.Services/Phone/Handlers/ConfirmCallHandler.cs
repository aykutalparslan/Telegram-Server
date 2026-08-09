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

    // One reflector row per call; any nonzero id maps to reflector index 1.
    private const long ReflectorConnectionId = 1;

    private readonly PrivacyEvaluator _privacy;
    private readonly ICallMediaRelay _relay;
    private readonly CallTurnConnectionBuilder _turn;

    public ConfirmCallHandler(IUnitOfWork unitOfWork, IBlockedPeersRepository blockedPeersRepository, IAuthorizationRepository authorizationRepository, IUserRepository userRepository, ICallRegistry registry,
        IUpdatesService updates, IMTProtoTime time, PrivacyEvaluator privacy,
        ICallMediaRelay relay, CallTurnConnectionBuilder turn)
        : base(unitOfWork, blockedPeersRepository, authorizationRepository, userRepository, registry, updates, time)
    {
        _privacy = privacy;
        _relay = relay;
        _turn = turn;
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

        // The caller commits to g_a via g_a_hash in requestCall; verify the
        // revealed g_a matches that commitment before finalizing.
        if (!CommitmentMatches(gA, existing.GAHash))
        {
            return Error(400, "GA_INVALID"u8);
        }

        switch (existing.State)
        {
            case CallSessionState.Confirmed:
                // Idempotent: return the existing immutable final call without a
                // second reflector allocation or fresh credentials.
                return BuildResult(BuildFinalCall(existing), existing.CallerUserId,
                    existing.CalleeUserId);
            case CallSessionState.Discarded:
                return Error(400, "CALL_ALREADY_DECLINED"u8);
            case CallSessionState.Requested:
            case CallSessionState.Received:
                return Error(400, "CALL_PEER_INVALID"u8);
        }

        // udp_p2p requires both a negotiated P2P offer and bilateral privacy so
        // direct IP disclosure is limited to mutually-allowing participants.
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
                // A concurrent confirm won; it shares this reflector allocation
                // (allocation is keyed by call id), so nothing to compensate.
                return BuildResult(BuildFinalCall(result.Call!),
                    result.Call!.CallerUserId, result.Call.CalleeUserId);
            case CallRegistryStatus.AlreadyDiscarded:
            case CallRegistryStatus.InvalidState:
                // Lost a discard/timeout race after allocating: compensate.
                _relay.RemoveAllocation(callId);
                return Error(400, "CALL_ALREADY_DECLINED"u8);
            default:
                _relay.RemoveAllocation(callId);
                return Error(400, "CALL_PEER_INVALID"u8);
        }

        CallSnapshot call = result.Call!;
        byte[] finalCall = BuildFinalCall(call);
        // The winning callee device receives the perspective-equivalent final
        // call carrying g_a and the shared connection set.
        if (call.CalleeAuthKeyId is long calleeAuthKey)
        {
            await PushCallUpdate(call.CalleeUserId, finalCall,
                UpdateDeliveryScope.ForAuthKey(calleeAuthKey));
        }

        return BuildResult(finalCall, call.CallerUserId, call.CalleeUserId);
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
