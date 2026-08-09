// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Services.Calls;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.phone;
using Ferrite.Utils;
using TLDto = Ferrite.TL.baseLayer.dto;
using TLUpdatesResult = Ferrite.TL.baseLayer.TLUpdates;

namespace Ferrite.Services.Phone.Handlers;

/// <summary>
/// phone.joinGroupCallPresentation. A screen share is a SECOND transport on an
/// existing camera join: it allocates no participant of its
/// own, never collides with the canonical audio source, and the camera join
/// survives it entirely.
/// </summary>
public sealed class JoinGroupCallPresentationHandler : GroupCallHandlerBase
{
    private readonly IGroupCallsRepository _groupCallsRepository;

    private readonly IGroupCallMediaPlane _media;

    public JoinGroupCallPresentationHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IAuthorizationRepository authorizationRepository, IGroupCallsRepository groupCallsRepository, UpdateFanout fanout,
        GroupCallChatLink chatLink, IUpdatesContextFactory updatesContexts,
        IMTProtoTime time, GroupCallVideoOptions videoOptions,
        GroupCallMediaSourceMap sourceMap, ILogger log, IGroupCallMediaPlane media)
        : base(unitOfWork, chatParticipantsRepository, chatRepository, authorizationRepository, groupCallsRepository, fanout, chatLink, updatesContexts, time, videoOptions,
            sourceMap, log)
    {
        _groupCallsRepository = groupCallsRepository;

        _media = media;
    }

    [TLFunction(Constructors.baseLayer_JoinGroupCallPresentation)]
    public async ValueTask<TLUpdatesResult> Handle(long authKeyId, TLBytes q)
    {
        var request = (JoinGroupCallPresentation)q;
        bool callRead = TryReadInputGroupCall(request.Get_CallView(), out long callId,
            out long accessHash);
        byte[] paramsJson = request.Get_ParamsPropertyView().Is(out DataJSON json)
            ? json.Data.ToArray()
            : Array.Empty<byte>();

        if (!callRead)
        {
            return Error(GroupCallErrors.GroupCallInvalid);
        }

        using GroupCallResolution resolution = await ResolveCallAsync(authKeyId, callId,
            accessHash, GroupCallAccessLevel.Participate);
        if (resolution.Error != null)
        {
            return Error(resolution.Error);
        }

        GroupCallPeerAccess access = resolution.Access!;

        // A screen share extends an existing join; there is nothing to attach it
        // to without one.
        string? mediaId = await GetMediaIdAsync(callId, access.CurrentUserId);
        if (mediaId == null)
        {
            return Error(GroupCallErrors.GroupCallJoinMissing);
        }

        GroupCallJoinPayload payload;
        try
        {
            payload = GroupCallJoinPayloadCodec.ParseJoinPayload(paramsJson);
        }
        catch (GroupCallDataJsonException e)
        {
            Log.Debug($"📞 joinGroupCallPresentation rejected the payload for " +
                      $"call:{callId} user:{access.CurrentUserId}: {e.Message}");
            return Error(GroupCallErrors.DataJsonInvalid);
        }

        GroupCallMediaJoinResult joined;
        try
        {
            joined = await _media.JoinPresentationAsync(new GroupCallMediaJoinRequest(
                callId, mediaId, payload));
        }
        catch (GroupCallMediaException e)
        {
            Log.Warning(e, $"📞 joinGroupCallPresentation media join failed for " +
                           $"call:{callId} user:{access.CurrentUserId} kind:{e.Kind}");
            return Error(e.Kind == GroupCallMediaFailureKind.Rejected
                ? GroupCallErrors.DataJsonInvalid
                : GroupCallErrors.MediaUnavailable);
        }

        string? endpoint = ResolvePresentationEndpoint(joined, mediaId);
        GroupCallParticipantEditResult stored = await _groupCallsRepository
            .TrySetParticipantPresentationAsync(callId, access.CurrentUserId, endpoint);
        if (stored.Status is not (GroupCallParticipantEditStatus.Updated or
            GroupCallParticipantEditStatus.NoChange))
        {
            stored.Participant?.Dispose();
            stored.Call?.Dispose();
            // Everything after the media call compensates: the client must never
            // be left with a live screen-share transport Ferrite has no row for.
            await ReleasePresentationAsync(callId, mediaId);
            return Error(stored.Status == GroupCallParticipantEditStatus.NotJoined
                ? GroupCallErrors.GroupCallJoinMissing
                : GroupCallErrors.GroupCallInvalid);
        }

        await UnitOfWork.SaveAsync();
        SourceMap.Replace(callId, joined.ViewerSources);

        byte[] connectionParams = GroupCallJoinPayloadCodec.BuildConnectionParams(
            joined.Transport);
        var updates = new List<byte[]>(3);
        // The presentation flag is how the client tells this credential set apart
        // from the camera one it already holds, so it leads the result.
        using (TLUpdate connection = GroupCallBuilders.BuildConnectionUpdate(
                   connectionParams, presentation: true))
        {
            updates.Add(connection.AsSpan().ToArray());
        }

        // A repeated presentation join re-issues credentials without a version
        // step, so there is no participant row to publish for it.
        if (stored.Status == GroupCallParticipantEditStatus.Updated)
        {
            using TLDto.TLGroupCallParticipantState participant = stored.Participant!.Value;
            using TLDto.TLGroupCallState updatedCall = stored.Call!.Value;
            int videoCount = await CountUnmutedVideoAsync(callId);
            GroupCallViewer viewer = await BuildViewerAsync(callId, access.CurrentUserId,
                access.CanManageCall);
            var selfOverlay = new GroupCallParticipantOverlay(MutedByYou: false,
                LocalVolume: null, Source: 0,
                BuildSelfSources(joined, payload, endpoint));
            using (TLGroupCallParticipant selfRow = GroupCallBuilders.BuildParticipant(
                       participant, viewer, selfOverlay,
                       GroupCallParticipantDecoration.Versioned))
            using (TLUpdate participants = BuildParticipantsUpdate(updatedCall,
                       selfRow.AsSpan()))
            {
                updates.Add(participants.AsSpan().ToArray());
            }
            updates.Add(BuildCallUpdateBytes(updatedCall, viewer, access.Peer.Id,
                videoCount));
            await PushPresentationToOtherMembersAsync(updatedCall, participant, access,
                videoCount);
        }

        Log.Debug($"📞 joinGroupCallPresentation call:{callId} " +
                  $"user:{access.CurrentUserId} media:{mediaId} endpoint:{endpoint}");
        return await BuildUnsequencedResultAsync(access.CurrentUserId, updates,
            access.ChatBytes!);
    }

    private static TLUpdatesResult Error(string message) =>
        (TLUpdatesResult)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));

    /// <summary>
    /// The endpoint every viewer's presentation row is keyed to. The worker states
    /// it in the video half of the answer; when a room has other viewers it also
    /// appears in their mappings, which is the fallback for a worker that answers
    /// without a video section.
    /// </summary>
    private static string? ResolvePresentationEndpoint(GroupCallMediaJoinResult joined,
        string producerMediaId)
    {
        if (joined.Transport.Video is { } video)
        {
            return video.Endpoint;
        }

        foreach (var producers in joined.ViewerSources.Values)
        {
            if (producers.TryGetValue(producerMediaId, out GroupCallViewerSources? sources) &&
                sources.Presentation is { } presentation)
            {
                return presentation.Endpoint;
            }
        }

        return null;
    }

    /// <summary>
    /// What the sharer sees of its own screen: the endpoint the worker assigned,
    /// against the source groups it just advertised. Its camera row is left to the
    /// mapping it already has, since this join does not touch it.
    /// </summary>
    private static GroupCallViewerSources? BuildSelfSources(GroupCallMediaJoinResult joined,
        GroupCallJoinPayload payload, string? endpoint)
    {
        if (endpoint == null)
        {
            return null;
        }

        return new GroupCallViewerSources(joined.CanonicalSource, Video: null,
            new GroupCallParticipantVideoSources(endpoint, payload.VideoSourceGroups,
                joined.CanonicalSource, Paused: false));
    }

    private async Task PushPresentationToOtherMembersAsync(TLDto.TLGroupCallState call,
        TLDto.TLGroupCallParticipantState participant, GroupCallPeerAccess access,
        int videoCount)
    {
        long callId = call.AsGroupCallState().Id;
        var view = participant.AsGroupCallParticipantState();
        long sharerUserId = view.UserId;
        string producerMediaId = Encoding.UTF8.GetString(view.MediaId);

        await Fanout.PushGroupCallUpdatesAsync(access.Peer.Id, access.CurrentUserId,
            async memberId =>
            {
                bool canManage = await CanManageCallAsync(access.Peer.Id, memberId);
                GroupCallViewer viewer = await BuildViewerAsync(callId, memberId,
                    canManage);
                string? viewerMediaId = await GetMediaIdAsync(callId, memberId);
                // Carries the member's stored local mute/volume for the sharer: a
                // non-min row overwrites the client's local state.
                GroupCallParticipantOverlay overlay = await BuildMemberOverlayAsync(
                    callId, memberId, viewerMediaId, sharerUserId, producerMediaId);
                using TLGroupCallParticipant row = GroupCallBuilders.BuildParticipant(
                    participant, viewer, overlay,
                    GroupCallParticipantDecoration.Versioned);
                return BuildParticipantsUpdate(call, row.AsSpan());
            });
        await PushCallUpdateToOtherMembersAsync(call, access.Peer.Id,
            access.CurrentUserId, videoCount);
    }

    private async Task ReleasePresentationAsync(long callId, string mediaId)
    {
        try
        {
            await _media.LeavePresentationAsync(callId, mediaId);
        }
        catch (GroupCallMediaException e)
        {
            Log.Warning(e, $"📞 joinGroupCallPresentation could not release the " +
                           $"uncommitted transport for call:{callId} media:{mediaId}");
        }
    }
}
