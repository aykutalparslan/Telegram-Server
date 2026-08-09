// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.Utils;

namespace Ferrite.Services.Calls;

/// <summary>
/// Runs the single terminal transition for a discarded call: it releases media
/// resources, writes the call-log service message into both private message
/// boxes exactly once, and delivers phoneCallDiscarded plus the log to the
/// participants. Both explicit discardCall and receive/ring timeout expiry
/// route through here so neither can double-fan-out or double-log.
/// </summary>
public sealed class CallTerminator
{
    private readonly ICallRegistry _registry;
    private readonly MessageStore _messageStore;
    private readonly IUpdatesService _updates;
    private readonly ICallMediaRelay _relay;
    private readonly CallSignalingLimiter _signaling;
    private readonly IMTProtoTime _time;
    private readonly IUnitOfWork _unitOfWork;

    public CallTerminator(ICallRegistry registry, MessageStore messageStore,
        IUpdatesService updates, ICallMediaRelay relay,
        CallSignalingLimiter signaling, IMTProtoTime time, IUnitOfWork unitOfWork)
    {
        _registry = registry;
        _messageStore = messageStore;
        _updates = updates;
        _relay = relay;
        _signaling = signaling;
        _time = time;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Finalizes a call whose registry state is already Discarded. When invoked
    /// by discardCall the invoker's own update copies are returned for the RPC
    /// result and only the peer's copies are enqueued; the timeout path passes a
    /// null invoker and both sides are enqueued.
    /// </summary>
    public async Task<List<byte[]>> FinalizeAsync(CallSnapshot call,
        long? invokerUserId, long? invokerAuthKeyId)
    {
        _relay.RemoveAllocation(call.CallId);
        _signaling.Remove(call.CallId);

        CallDiscardInfo discard = call.Discard
            ?? throw new InvalidOperationException("Call is not discarded.");
        int date = checked((int)_time.GetUnixTimeInSeconds());
        byte[] action = BuildCallLogAction(call, discard);
        byte[] discardedCall = BuildDiscarded(call, discard);
        // Guard belt-and-suspenders even though the first terminal transition
        // already runs this once: never write a second log entry.
        bool writeLog = _registry.TryMarkCallLogWritten(call.CallId).Status
            == CallRegistryStatus.Ok;

        var invokerUpdates = new List<byte[]>();
        await DeliverSideAsync(call, discardedCall, action, writeLog, date,
            isCaller: true, invokerUserId, invokerAuthKeyId, invokerUpdates);
        await DeliverSideAsync(call, discardedCall, action, writeLog, date,
            isCaller: false, invokerUserId, invokerAuthKeyId, invokerUpdates);

        await _unitOfWork.SaveAsync();
        return invokerUpdates;
    }

    private async Task DeliverSideAsync(CallSnapshot call, byte[] discardedCall,
        byte[] action, bool writeLog, int date, bool isCaller, long? invokerUserId,
        long? invokerAuthKeyId, List<byte[]> invokerUpdates)
    {
        long userId = isCaller ? call.CallerUserId : call.CalleeUserId;
        long dialogPeer = isCaller ? call.CalleeUserId : call.CallerUserId;
        bool isInvoker = invokerUserId == userId;

        // phoneCallDiscarded reaches the bound call device, or every callee
        // device when no winner was ever bound.
        UpdateDeliveryScope discardScope;
        if (isCaller)
        {
            discardScope = UpdateDeliveryScope.ForAuthKey(call.CallerAuthKeyId);
        }
        else
        {
            discardScope = call.CalleeAuthKeyId is long calleeKey
                ? UpdateDeliveryScope.ForAuthKey(calleeKey)
                : UpdateDeliveryScope.All;
        }

        StoredMessageWrite? logWrite = null;
        if (writeLog)
        {
            long? authKeyId = isInvoker ? invokerAuthKeyId : null;
            // The service message is authored by the caller so both boxes show
            // the correct outgoing/incoming perspective.
            logWrite = await _messageStore.PutPrivateServiceMessageAsync(userId,
                authKeyId, dialogPeer, call.CallerUserId, outgoing: isCaller,
                action, date);
        }

        if (isInvoker)
        {
            invokerUpdates.Add(BuildUpdatePhoneCall(discardedCall));
            if (logWrite is { } write)
            {
                invokerUpdates.Add(BuildUpdateNewMessage(write));
            }

            return;
        }

        await EnqueuePhoneCallAsync(userId, discardedCall, discardScope);
        if (logWrite is { } peerWrite)
        {
            await EnqueueNewMessageAsync(userId, peerWrite);
        }
    }

    private async Task EnqueuePhoneCallAsync(long userId, byte[] discardedCall,
        UpdateDeliveryScope scope)
    {
        TLUpdate update = UpdatePhoneCall.Builder().PhoneCall(discardedCall).Build();
        await _updates.EnqueueUpdate(userId, update, scope);
    }

    private async Task EnqueueNewMessageAsync(long userId, StoredMessageWrite write)
    {
        // The call-log service message is an ordinary private message, so it
        // reaches every one of the user's devices.
        TLUpdate update = UpdateNewMessage.Builder()
            .Message(write.Bytes)
            .Pts(write.Pts)
            .PtsCount(1)
            .Build();
        await _updates.EnqueueUpdate(userId, update, UpdateDeliveryScope.All);
    }

    private static byte[] BuildUpdatePhoneCall(byte[] discardedCall)
    {
        using var update = UpdatePhoneCall.Builder().PhoneCall(discardedCall).Build();
        return update.ToReadOnlySpan().ToArray();
    }

    private static byte[] BuildUpdateNewMessage(StoredMessageWrite write)
    {
        using var update = UpdateNewMessage.Builder()
            .Message(write.Bytes)
            .Pts(write.Pts)
            .PtsCount(1)
            .Build();
        return update.ToReadOnlySpan().ToArray();
    }

    private static byte[] BuildDiscarded(CallSnapshot call, CallDiscardInfo discard)
    {
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

        using var built = builder.Build();
        return built.ToReadOnlySpan().ToArray();
    }

    private static byte[] BuildCallLogAction(CallSnapshot call, CallDiscardInfo discard)
    {
        using TLPhoneCallDiscardReason reason =
            PhoneCallReasons.Build(discard.ReasonConstructor);
        var builder = MessageActionPhoneCall.Builder()
            .Video(call.Video)
            .CallId(call.CallId)
            .Reason(reason.AsSpan());
        if (discard.Duration > 0)
        {
            builder = builder.Duration(discard.Duration);
        }

        using var built = builder.Build();
        return built.ToReadOnlySpan().ToArray();
    }
}
