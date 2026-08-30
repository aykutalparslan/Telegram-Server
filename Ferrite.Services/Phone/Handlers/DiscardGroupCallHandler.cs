// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
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
        var view = resolution.Call!.Value.AsGroupCallState();
        int startedDate = view.Flags[3] ? view.StartedDate : 0;
        int observedState = view.State;
        int recordingGeneration = view.Flags[9] && view.Flags[12]
            ? view.RecordingGeneration
            : 0;

        int now = Now();
        int duration = startedDate > 0 ? Math.Max(0, now - startedDate) : 0;

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
        SourceMap.Forget(callId);
        await EndRoomAsync(callId);
        await EndBroadcastAsync(callId);
        await CancelRecordingAsync(callId, recordingGeneration);
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
