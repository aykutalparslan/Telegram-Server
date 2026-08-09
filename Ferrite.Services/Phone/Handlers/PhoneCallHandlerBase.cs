// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Services.Calls;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.Utils;
using PhoneResult = Ferrite.TL.baseLayer.phone.TLPhoneCall;
using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Phone.Handlers;

/// <summary>
/// Shared mechanics for the 1:1 phone.* handlers: identity resolution, call
/// value/result construction, and exact-device update delivery. It holds no
/// endpoint behavior; each concrete handler owns its own transition.
/// </summary>
public abstract class PhoneCallHandlerBase
{
    private readonly IBlockedPeersRepository _blockedPeersRepository;

    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IUserRepository _userRepository;

    protected readonly IUnitOfWork UnitOfWork;
    protected readonly ICallRegistry Registry;
    protected readonly IUpdatesService Updates;
    protected readonly IMTProtoTime Time;
    protected readonly ILogger? Log;

    protected PhoneCallHandlerBase(IUnitOfWork unitOfWork, IBlockedPeersRepository blockedPeersRepository, IAuthorizationRepository authorizationRepository, IUserRepository userRepository, ICallRegistry registry,
        IUpdatesService updates, IMTProtoTime time, ILogger? log = null)
    {
        _blockedPeersRepository = blockedPeersRepository;

        _authorizationRepository = authorizationRepository;
        _userRepository = userRepository;

        UnitOfWork = unitOfWork;
        Registry = registry;
        Updates = updates;
        Time = time;
        Log = log;
    }

    protected static PhoneResult Error(int code, ReadOnlySpan<byte> message) =>
        (PhoneResult)RpcErrorGenerator.GenerateError(code, message);

    protected int Now() => checked((int)Time.GetUnixTimeInSeconds());

    protected async ValueTask<long?> GetCurrentUserIdAsync(long authKeyId)
    {
        TLDto.TLAuthInfo? authorization = await _authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        if (authorization is null)
        {
            return null;
        }

        using TLDto.TLAuthInfo ownedAuthorization = authorization.Value;
        TLDto.AuthInfo row = ownedAuthorization.AsAuthInfo();
        return row.LoggedIn ? row.UserId : null;
    }

    protected bool IsBlockedBy(long ownerUserId, long peerUserId)
    {
        foreach (TLDto.TLBlockedPeer blockedValue in _blockedPeersRepository.GetBlockedPeers(ownerUserId))
        {
            using (blockedValue)
            {
                TLDto.BlockedPeer row = blockedValue.AsBlockedPeer();
                if (row.PeerType == (int)PeerType.User && row.PeerId == peerUserId)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Reads a serialized phoneCallProtocol span into the transient
    /// CallProtocol record. Must run synchronously before any await because
    /// the span is backed by the request's pooled memory.
    /// </summary>
    protected static CallProtocol ReadProtocol(Span<byte> protocolSpan)
    {
        var concrete = (PhoneCallProtocol)protocolSpan;
        var versions = new List<string>();
        VectorOfString versionVector = concrete.LibraryVersions;
        int count = versionVector.Count;
        for (int i = 0; i < count; i++)
        {
            versions.Add(System.Text.Encoding.UTF8.GetString(
                versionVector.ReadTLBytes()));
        }

        return new CallProtocol(concrete.UdpP2p, concrete.UdpReflector,
            concrete.MinLayer, concrete.MaxLayer, versions);
    }

    protected static byte[] BuildProtocol(CallProtocol protocol)
    {
        var versions = new VectorOfString();
        foreach (string version in protocol.LibraryVersions)
        {
            versions.AppendTLBytes(System.Text.Encoding.UTF8.GetBytes(version));
        }

        using var built = PhoneCallProtocol.Builder()
            .UdpP2p(protocol.UdpP2p)
            .UdpReflector(protocol.UdpReflector)
            .MinLayer(protocol.MinLayer)
            .MaxLayer(protocol.MaxLayer)
            .LibraryVersions(versions)
            .Build();
        return built.ToReadOnlySpan().ToArray();
    }

    protected static byte[] BuildWaiting(CallSnapshot call)
    {
        byte[] protocol = BuildProtocol(call.CallerProtocol);
        var builder = PhoneCallWaiting.Builder()
            .Video(call.Video)
            .Id(call.CallId)
            .AccessHash(call.AccessHash)
            .Date(call.Date)
            .AdminId(call.CallerUserId)
            .ParticipantId(call.CalleeUserId)
            .Protocol(protocol);
        if (call.ReceiveDate is int receiveDate)
        {
            builder = builder.ReceiveDate(receiveDate);
        }

        using var built = builder.Build();
        return built.ToReadOnlySpan().ToArray();
    }

    protected static byte[] BuildRequested(CallSnapshot call)
    {
        byte[] protocol = BuildProtocol(call.CallerProtocol);
        using var built = PhoneCallRequested.Builder()
            .Video(call.Video)
            .Id(call.CallId)
            .AccessHash(call.AccessHash)
            .Date(call.Date)
            .AdminId(call.CallerUserId)
            .ParticipantId(call.CalleeUserId)
            .GAHash(call.GAHash)
            .Protocol(protocol)
            .Build();
        return built.ToReadOnlySpan().ToArray();
    }

    protected static byte[] BuildAccepted(CallSnapshot call)
    {
        byte[] protocol = BuildProtocol(call.NegotiatedProtocol ?? call.CallerProtocol);
        using var built = PhoneCallAccepted.Builder()
            .Video(call.Video)
            .Id(call.CallId)
            .AccessHash(call.AccessHash)
            .Date(call.Date)
            .AdminId(call.CallerUserId)
            .ParticipantId(call.CalleeUserId)
            .GB(call.GB ?? Array.Empty<byte>())
            .Protocol(protocol)
            .Build();
        return built.ToReadOnlySpan().ToArray();
    }

    protected static byte[] BuildDiscarded(long callId, int reasonConstructor,
        bool video, int duration)
    {
        byte[] reason = BuildDiscardReason(reasonConstructor);
        var builder = PhoneCallDiscarded.Builder()
            .Video(video)
            .Id(callId)
            .Reason(reason);
        if (duration > 0)
        {
            builder = builder.Duration(duration);
        }

        using var built = builder.Build();
        return built.ToReadOnlySpan().ToArray();
    }

    protected static byte[] BuildDiscardReason(int reasonConstructor)
    {
        using TLPhoneCallDiscardReason reason =
            Ferrite.Services.Calls.PhoneCallReasons.Build(reasonConstructor);
        return reason.AsSpan().ToArray();
    }

    /// <summary>
    /// Builds a phone.phoneCall result carrying the serialized call plus both
    /// participant user rows.
    /// </summary>
    protected PhoneResult BuildResult(byte[] callBytes, long callerUserId,
        long calleeUserId)
    {
        var users = BuildParticipantUsers(callerUserId, calleeUserId);
        return Ferrite.TL.baseLayer.phone.PhonePhoneCall.Builder()
            .PhoneCall(callBytes)
            .Users(users)
            .Build();
    }

    /// <summary>
    /// Builds a Vector of both participant user rows. Vector is a ref struct;
    /// the append helper must take it by ref so the regrown buffer and advanced
    /// offset are visible. Passing it by value bumps the count through the
    /// shared backing array but drops the element bytes, yielding a truncated
    /// users vector that clients reject with "Wrong vector length".
    /// </summary>
    protected Vector BuildParticipantUsers(long callerUserId, long calleeUserId)
    {
        var users = new Vector();
        AppendUser(ref users, callerUserId);
        AppendUser(ref users, calleeUserId);
        return users;
    }

    private void AppendUser(ref Vector users, long userId)
    {
        using TLUser? user = _userRepository.GetUser(userId);
        if (user != null)
        {
            users.AppendTLObject(user.Value.AsSpan());
        }
    }

    protected async Task PushCallUpdate(long userId, byte[] callBytes,
        UpdateDeliveryScope scope)
    {
        TLUpdate update = UpdatePhoneCall.Builder().PhoneCall(callBytes).Build();
        await Updates.EnqueueUpdate(userId, update, scope);
    }

    protected static (long Id, long AccessHash) ReadInputPhoneCall(Span<byte> peerSpan)
    {
        var peer = (InputPhoneCall)peerSpan;
        return (peer.Id, peer.AccessHash);
    }

    /// <summary>
    /// Resolves a call the user participates in, including a short-lived
    /// discarded tombstone, validating the access hash. Returns null when the
    /// call is unknown, the hash mismatches, or the user is not a participant.
    /// </summary>
    protected CallSnapshot? ResolveParticipantCall(long callId,
        long accessHash, long userId)
    {
        CallSnapshot? call = Registry.Get(callId);
        if (call is null || call.AccessHash != accessHash)
        {
            return null;
        }

        if (call.CallerUserId != userId && call.CalleeUserId != userId)
        {
            return null;
        }

        return call;
    }

    /// <summary>
    /// Builds the serialized final phoneCall carried in the caller's result and
    /// the callee's update. Both sides share one value: g_a_or_b is g_a, the
    /// key fingerprint is the client-supplied value, and the connection set and
    /// protocol are identical.
    /// </summary>
    protected static byte[] BuildFinalCall(CallSnapshot call)
    {
        byte[] protocol = BuildProtocol(call.NegotiatedProtocol ?? call.CallerProtocol);
        var connections = new Vector();
        if (call.Connections != null)
        {
            foreach (byte[] connection in call.Connections)
            {
                connections.AppendTLObject(connection);
            }
        }

        var builder = PhoneCall.Builder()
            .P2pAllowed(call.P2pAllowed)
            .Video(call.Video)
            .Id(call.CallId)
            .AccessHash(call.AccessHash)
            .Date(call.Date)
            .AdminId(call.CallerUserId)
            .ParticipantId(call.CalleeUserId)
            .GAOrB(call.GA ?? Array.Empty<byte>())
            .KeyFingerprint(call.KeyFingerprint ?? 0)
            .Protocol(protocol)
            .Connections(connections)
            .StartDate(call.StartDate ?? 0);
        using var built = builder.Build();
        return built.ToReadOnlySpan().ToArray();
    }

    /// <summary>
    /// Builds one reflector phoneConnection row. The id must be nonzero (the
    /// client ranks reflector ids to derive a 1-based reflector index), and the
    /// 16-byte peer tag is shared by both parties.
    /// </summary>
    protected static byte[] BuildReflectorConnection(long id, string ip, int port,
        ReadOnlySpan<byte> peerTag)
    {
        using var built = PhoneConnection.Builder()
            .Id(id)
            .Ip(System.Text.Encoding.UTF8.GetBytes(ip))
            .Ipv6(""u8)
            .Port(port)
            .PeerTag(peerTag)
            .Build();
        return built.ToReadOnlySpan().ToArray();
    }
}
