// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Services.Calls;
using Ferrite.Services.Calls.E2E;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.phone;
using Ferrite.Utils;
using TLDto = Ferrite.TL.baseLayer.dto;
using TLUpdatesResult = Ferrite.TL.baseLayer.TLUpdates;

namespace Ferrite.Services.Phone.Handlers;

/// <summary>
/// phone.discardGroupCall. Ends the call once: the terminal row, the chat unlink,
/// the ended action message, and the worker teardown all happen on the transition
/// only. A retry replays the same terminal result and never recreates the room.
/// </summary>
public sealed class DiscardGroupCallHandler : GroupCallHandlerBase
{
    private readonly IGroupCallsRepository _groupCallsRepository;

    private readonly GroupCallActionMessages _actions;
    private readonly GroupCallActivityTracker _activity;
    private readonly IGroupCallMediaPlane _media;
    private readonly IGroupCallBroadcastPlane _broadcast;
    private readonly IGroupCallRecordingCoordinator _recording;
    private readonly IGroupCallChainService _chain;

    public DiscardGroupCallHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IAuthorizationRepository authorizationRepository, IGroupCallsRepository groupCallsRepository, UpdateFanout fanout,
        GroupCallChatLink chatLink, IUpdatesContextFactory updatesContexts,
        IMTProtoTime time, GroupCallVideoOptions videoOptions,
        GroupCallMediaSourceMap sourceMap, ILogger log,
        GroupCallActionMessages actions, GroupCallActivityTracker activity,
        IGroupCallMediaPlane media, IGroupCallBroadcastPlane broadcast,
        IGroupCallRecordingCoordinator recording, IGroupCallChainService chain)
        : base(unitOfWork, chatParticipantsRepository, chatRepository, authorizationRepository, groupCallsRepository, fanout, chatLink, updatesContexts, time, videoOptions, sourceMap, log)
    {
        _groupCallsRepository = groupCallsRepository;

        _actions = actions;
        _activity = activity;
        _media = media;
        _broadcast = broadcast;
        _recording = recording;
        _chain = chain;
    }

    [TLFunction(Constructors.baseLayer_DiscardGroupCall)]
    public async ValueTask<TLUpdatesResult> Handle(long authKeyId, TLBytes q)
    {
        var request = (DiscardGroupCall)q;
        bool callRead = TryReadInputGroupCall(request.Get_CallView(), out long callId,
            out long accessHash);

        if (!callRead)
        {
            return Error(GroupCallErrors.GroupCallInvalid);
        }

        using GroupCallResolution resolution = await ResolveCallAsync(authKeyId, callId,
            accessHash, GroupCallAccessLevel.Manage);
        if (resolution.Error != null)
        {
            return Error(resolution.Error);
        }

        GroupCallPeerAccess access = resolution.Access!;
        // The terminal row keeps the monotonic generation but drops the active
        // recording flags, so the abandoned session is identified before discard.
        var view = resolution.Call!.Value.AsGroupCallState();
        int startedDate = view.Flags[3] ? view.StartedDate : 0;
        int observedState = view.State;
        int recordingGeneration = view.Flags[9] && view.Flags[12]
            ? view.RecordingGeneration
            : 0;

        int now = Now();
        // A scheduled call that never started has no elapsed time to report.
        int duration = startedDate > 0 ? Math.Max(0, now - startedDate) : 0;

        // A conference's audience IS its participant list, and the discard clears
        // it, so the members are read before the transition rather than after.
        List<long> conferenceMembers = access.IsConference
            ? await GetConferenceMemberIdsAsync(callId, access.CurrentUserId)
            : new List<long>();

        GroupCallDiscardResult discarded = await _groupCallsRepository
            .TryDiscardCallAsync(callId, now, duration,
                expectedState: observedState);
        switch (discarded.Status)
        {
            case GroupCallDiscardStatus.Discarded:
                break;
            case GroupCallDiscardStatus.AlreadyDiscarded:
                using (TLDto.TLGroupCallState terminal = discarded.Call!.Value)
                {
                    return await BuildTerminalReplayAsync(authKeyId, access, terminal);
                }
            default:
                return Error(GroupCallErrors.GroupCallInvalid);
        }

        using TLDto.TLGroupCallState call = discarded.Call!.Value;
        _activity.Forget(callId);
        // The per-viewer SSRC mapping is live worker state; it dies with the room.
        SourceMap.Forget(callId);
        await EndRoomAsync(callId);
        await EndBroadcastAsync(callId);
        await CancelRecordingAsync(callId, recordingGeneration);
        // A conference's chain is call-scoped state with no other owner, so the
        // discard is also what drops it. Nothing retains chain history after a
        // call ends.
        if (access.IsConference)
        {
            await _chain.DiscardAsync(callId);
            return await BuildConferenceDiscardAsync(authKeyId, access, call,
                conferenceMembers, duration);
        }

        byte[] chatBytes = ChatLink.SetCallFlags(access.Kind, access.ChatBytes!,
            callActive: false, callNotEmpty: false);

        GroupCallViewer viewer = await BuildViewerAsync(callId, access.CurrentUserId,
            access.CanManageCall);
        // A discarded call keeps no participant rows, so nobody is publishing.
        byte[] callUpdate = BuildCallUpdateBytes(call, viewer, access.Peer.Id,
            unmutedVideoCount: 0);
        await PushCallUpdateToOtherMembersAsync(call, access.Peer.Id,
            access.CurrentUserId, unmutedVideoCount: 0);

        byte[] actionBytes = BuildEndedActionBytes(callId, accessHash, duration);
        Log.Debug($"📞 discardGroupCall call:{callId} peer:{access.Peer.Type}/" +
                  $"{access.Peer.Id} by:{access.CurrentUserId} duration:{duration}s");
        return await _actions.EmitAsync(authKeyId, access.CurrentUserId, access.Kind,
            access.Peer.Id, chatBytes, actionBytes, new[] { callUpdate });
    }

    private static TLUpdatesResult Error(string message) =>
        (TLUpdatesResult)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));

    /// <summary>
    /// Worker teardown is best-effort and idempotent: the call is already terminal
    /// in Ferrite, so a worker that is down must not turn a successful discard into
    /// an error the client would retry.
    /// </summary>
    private async Task EndRoomAsync(long callId)
    {
        try
        {
            await _media.EndRoomAsync(callId);
        }
        catch (GroupCallMediaException e)
        {
            Log.Warning(e, $"📞 discardGroupCall could not tear down the room for " +
                           $"call:{callId}; the call is already terminal");
        }
    }

    private async Task EndBroadcastAsync(long callId)
    {
        try
        {
            await _broadcast.EndStreamAsync(callId);
        }
        catch (GroupCallBroadcastException e)
        {
            Log.Warning(e, $"📡 discardGroupCall could not tear down broadcast " +
                           $"for call:{callId}; the call is already terminal");
        }
    }

    /// <summary>
    /// Discarding abandons any recording in progress: its durable intent is gone
    /// with the terminal row, so nothing will ever finalize or acknowledge it.
    /// Without this the worker keeps the session and its file until the bounded
    /// duration cap, holding one of the configured recording slots. The
    /// coordinator skips a session a concurrent stop is already finalizing.
    /// </summary>
    private Task CancelRecordingAsync(long callId, int generation) =>
        _recording.TryCancelAsync(callId, generation).AsTask();

    private static byte[] BuildEndedActionBytes(long callId, long accessHash,
        int duration)
    {
        using TLInputGroupCall inputCall = GroupCallBuilders.BuildInputGroupCall(callId,
            accessHash);
        using TLMessageAction action = GroupCallActionMessages.BuildCallAction(inputCall,
            duration);
        return action.AsSpan().ToArray();
    }

    /// <summary>
    /// A conference's discard. There is no chat to unlink and no dialog to write
    /// an ended action into, so the terminal call update IS the whole
    /// announcement, and it goes to the participants the call had a moment ago.
    /// </summary>
    private async ValueTask<TLUpdatesResult> BuildConferenceDiscardAsync(long authKeyId,
        GroupCallPeerAccess access, TLDto.TLGroupCallState call,
        IReadOnlyList<long> members, int duration)
    {
        long callId = call.AsGroupCallState().Id;
        GroupCallViewer viewer = await BuildViewerAsync(callId, access.CurrentUserId,
            access.CanManageCall);
        byte[] callUpdate = BuildConferenceCallUpdateBytes(call, viewer,
            unmutedVideoCount: 0);

        int delivered = await Fanout.PushGroupCallUpdatesToAsync(members,
            async memberId =>
            {
                GroupCallViewer memberViewer = await BuildViewerAsync(callId, memberId,
                    canManageCall: memberId == access.Peer.Id);
                using Ferrite.TL.baseLayer.TLGroupCall built = GroupCallBuilders
                    .BuildCall(call, memberViewer, VideoOptions, unmutedVideoCount: 0);
                return UpdateGroupCall.Builder().Call(built.AsSpan()).Build();
            });

        Log.Debug($"📞 discardGroupCall conference call:{callId} " +
                  $"by:{access.CurrentUserId} duration:{duration}s fanout:{delivered}");
        return await BuildConferenceResultAsync(authKeyId, access.CurrentUserId,
            new[] { callUpdate });
    }

    /// <summary>
    /// The stable answer to a repeated discard: the same terminal
    /// <c>groupCallDiscarded</c> update, with no second action message, chat
    /// unlink, fan-out, or worker call.
    /// </summary>
    private async ValueTask<TLUpdatesResult> BuildTerminalReplayAsync(long authKeyId,
        GroupCallPeerAccess access, TLDto.TLGroupCallState call)
    {
        long callId = call.AsGroupCallState().Id;
        GroupCallViewer viewer = await BuildViewerAsync(callId, access.CurrentUserId,
            access.CanManageCall);
        if (access.IsConference)
        {
            return await BuildConferenceResultAsync(authKeyId, access.CurrentUserId,
                new[]
                {
                    BuildConferenceCallUpdateBytes(call, viewer, unmutedVideoCount: 0)
                });
        }

        byte[] callUpdate = BuildCallUpdateBytes(call, viewer, access.Peer.Id,
            unmutedVideoCount: 0);
        return await BuildInvokerResultAsync(authKeyId, access.CurrentUserId,
            new[] { callUpdate }, access.ChatBytes!);
    }
}
