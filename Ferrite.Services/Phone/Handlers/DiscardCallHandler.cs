// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Services.Calls;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.phone;
using Ferrite.Utils;
using TLUpdatesResult = Ferrite.TL.baseLayer.TLUpdates;

namespace Ferrite.Services.Phone.Handlers;

public sealed class DiscardCallHandler : PhoneCallHandlerBase
{
    private readonly CallTerminator _terminator;
    private readonly IUpdatesContextFactory _updatesContextFactory;

    public DiscardCallHandler(IUnitOfWork unitOfWork, IBlockedPeersRepository blockedPeersRepository, IAuthorizationRepository authorizationRepository, IUserRepository userRepository, ICallRegistry registry,
        IUpdatesService updates, IMTProtoTime time, CallTerminator terminator,
        IUpdatesContextFactory updatesContextFactory)
        : base(unitOfWork, blockedPeersRepository, authorizationRepository, userRepository, registry, updates, time)
    {
        _terminator = terminator;
        _updatesContextFactory = updatesContextFactory;
    }

    [TLFunction(Constructors.baseLayer_DiscardCall)]
    public async ValueTask<TLUpdatesResult> Handle(long authKeyId, TLBytes q)
    {
        var request = (DiscardCall)q;
        (long callId, long accessHash) = ReadInputPhoneCall(request.Peer);
        int duration = request.Duration;
        long connectionId = request.ConnectionId;
        int reasonConstructor = request.Get_ReasonView().Constructor;

        long? userIdValue = await GetCurrentUserIdAsync(authKeyId);
        if (userIdValue is not long userId)
        {
            return (TLUpdatesResult)RpcErrorGenerator.GenerateError(400,
                "AUTH_KEY_INVALID"u8);
        }

        int date = Now();
        CallRegistryResult result = Registry.TryDiscard(callId, accessHash, userId,
            authKeyId, reasonConstructor, duration, connectionId, date);
        switch (result.Status)
        {
            case CallRegistryStatus.Ok:
                break;
            case CallRegistryStatus.AlreadyDiscarded:
                // Idempotent: return the discarded update again without a second
                // fan-out or call-log entry.
                return await BuildUpdatesAsync(result.Call!, authKeyId, userId,
                    new List<byte[]> { DiscardedUpdate(result.Call!) });
            case CallRegistryStatus.NotFound:
            case CallRegistryStatus.AccessHashInvalid:
            case CallRegistryStatus.WrongUser:
            case CallRegistryStatus.WrongDevice:
                return (TLUpdatesResult)RpcErrorGenerator.GenerateError(400,
                    "CALL_PEER_INVALID"u8);
            default:
                return (TLUpdatesResult)RpcErrorGenerator.GenerateError(400,
                    "CALL_PEER_INVALID"u8);
        }

        CallSnapshot call = result.Call!;
        List<byte[]> invokerUpdates = await _terminator.FinalizeAsync(call, userId,
            authKeyId);
        return await BuildUpdatesAsync(call, authKeyId, userId, invokerUpdates);
    }

    private async ValueTask<TLUpdatesResult> BuildUpdatesAsync(CallSnapshot call,
        long authKeyId, long userId, List<byte[]> updateBytes)
    {
        var seqContext = _updatesContextFactory.GetUpdatesContext(authKeyId, userId);
        int seq = await seqContext.IncrementSeq();
        var updatesVector = new Vector();
        foreach (byte[] update in updateBytes)
        {
            updatesVector.AppendTLObject(update);
        }

        var users = BuildParticipantUsers(call.CallerUserId, call.CalleeUserId);
        // `Updates` alone binds to the inherited IUpdatesService field, so the
        // TL builder must be qualified.
        return Ferrite.TL.baseLayer.Updates.Builder()
            .UpdatesProperty(updatesVector)
            .Users(users)
            .Chats(new Vector())
            .Date(Now())
            .Seq(seq)
            .Build();
    }

    private static byte[] DiscardedUpdate(CallSnapshot call)
    {
        CallDiscardInfo discard = call.Discard!;
        using TLPhoneCallDiscardReason reason =
            PhoneCallReasons.Build(discard.ReasonConstructor);
        var builder = PhoneCallDiscarded.Builder()
            .Video(call.Video)
            .Id(call.CallId)
            .Reason(reason.AsSpan());
        if (discard.Duration > 0)
        {
            builder = builder.Duration(discard.Duration);
        }

        using var discarded = builder.Build();
        using var update = UpdatePhoneCall.Builder()
            .PhoneCall(discarded.ToReadOnlySpan())
            .Build();
        return update.ToReadOnlySpan().ToArray();
    }
}
