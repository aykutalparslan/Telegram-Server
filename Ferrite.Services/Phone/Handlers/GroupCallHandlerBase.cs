// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Services.Calls;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.Utils;
using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Phone.Handlers;

/// <summary>
/// One resolved group-call request: the owned call row plus the rights decision
/// for the hosting peer. <see cref="Error"/> is null exactly when both are set.
/// </summary>
public sealed class GroupCallResolution : IDisposable
{
    private GroupCallResolution(TLDto.TLGroupCallState? call, GroupCallPeerAccess? access,
        string? error)
    {
        Call = call;
        Access = access;
        Error = error;
    }

    public TLDto.TLGroupCallState? Call { get; }

    public GroupCallPeerAccess? Access { get; }

    public string? Error { get; }

    public static GroupCallResolution Failed(string error) => new(null, null, error);

    public static GroupCallResolution Resolved(TLDto.TLGroupCallState call,
        GroupCallPeerAccess access) => new(call, access, null);

    public void Dispose() => Call?.Dispose();
}

/// <summary>
/// Shared mechanics for the phone.*GroupCall handlers: resolving an
/// <c>InputGroupCall</c> to an authorized call row, building the viewer a row is
/// rendered for, and emitting viewer-correct <c>updateGroupCall</c>. It holds no
/// endpoint behavior; each concrete handler owns its own lifecycle transition.
/// </summary>
public abstract class GroupCallHandlerBase
{
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;

    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IGroupCallsRepository _groupCallsRepository;

    protected readonly IUnitOfWork UnitOfWork;
    protected readonly UpdateFanout Fanout;
    protected readonly GroupCallChatLink ChatLink;
    protected readonly IUpdatesContextFactory UpdatesContexts;
    protected readonly IMTProtoTime Time;
    protected readonly GroupCallVideoOptions VideoOptions;
    protected readonly GroupCallMediaSourceMap SourceMap;
    protected readonly ILogger Log;

    protected GroupCallHandlerBase(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IAuthorizationRepository authorizationRepository, IGroupCallsRepository groupCallsRepository, UpdateFanout fanout,
        GroupCallChatLink chatLink, IUpdatesContextFactory updatesContexts,
        IMTProtoTime time, GroupCallVideoOptions videoOptions,
        GroupCallMediaSourceMap sourceMap, ILogger log)
    {
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;

        _authorizationRepository = authorizationRepository;
        _groupCallsRepository = groupCallsRepository;

        UnitOfWork = unitOfWork;
        Fanout = fanout;
        ChatLink = chatLink;
        UpdatesContexts = updatesContexts;
        Time = time;
        VideoOptions = videoOptions;
        SourceMap = sourceMap;
        Log = log;
    }

    protected int Now() => checked((int)Time.GetUnixTimeInSeconds());

    /// <summary>
    /// Reads the only InputGroupCall variant Ferrite serves. The slug and
    /// invite-message variants address conference links, which are out of the
    /// audio-only scope, so they are rejected rather than silently misread. Must
    /// run synchronously: the view is a ref struct over the request's memory.
    /// </summary>
    protected static bool TryReadInputGroupCall(InputGroupCallView view, out long id,
        out long accessHash)
    {
        if (view.Is(out InputGroupCall call) && call.Id != 0)
        {
            id = call.Id;
            accessHash = call.AccessHash;
            return true;
        }

        id = 0;
        accessHash = 0;
        return false;
    }

    /// <summary>
    /// Resolves the call, validates its access hash, and runs the rights gate on
    /// the peer the call row itself names. A non-member is reported as
    /// GROUPCALL_FORBIDDEN rather than USER_NOT_PARTICIPANT because the call id is
    /// the resource the client asked about, and pinned TDLib drops its local call
    /// state on that error.
    /// </summary>
    protected async ValueTask<GroupCallResolution> ResolveCallAsync(long authKeyId,
        long callId, long accessHash, GroupCallAccessLevel level,
        CancellationToken cancellationToken = default)
    {
        TLDto.TLGroupCallState? call = await _groupCallsRepository
            .GetCallAsync(callId, cancellationToken);
        if (call == null)
        {
            return GroupCallResolution.Failed(GroupCallErrors.GroupCallInvalid);
        }

        var view = call.Value.AsGroupCallState();
        if (view.AccessHash != accessHash)
        {
            call.Value.Dispose();
            return GroupCallResolution.Failed(GroupCallErrors.GroupCallInvalid);
        }

        GroupCallPeerRef peer = new GroupCallPeerRef((GroupCallPeerType)view.PeerType, view.PeerId);
        bool conference = view.Conference;
        long creatorUserId = view.CreatorUserId;

        // A peerless conference reaches the shared endpoints too — checkGroupCall
        // above all, whose GROUPCALL_INVALID makes pinned TDLib decide it has been
        // dropped from the call ten seconds after joining. There is no chat to
        // authorize against, so the call's own membership is the gate.
        if (conference)
        {
            GroupCallPeerAccess conferenceAccess;
            try
            {
                conferenceAccess = await AuthorizeConferenceAsync(authKeyId, callId,
                    creatorUserId, level, cancellationToken);
            }
            catch
            {
                call.Value.Dispose();
                throw;
            }

            if (conferenceAccess.Error != null)
            {
                call.Value.Dispose();
                return GroupCallResolution.Failed(conferenceAccess.Error);
            }
            return GroupCallResolution.Resolved(call.Value, conferenceAccess);
        }

        GroupCallPeerAccess access;
        try
        {
            access = await GroupCallAccess.AuthorizeAsync(_authorizationRepository, _chatRepository, _chatParticipantsRepository, authKeyId, peer,
                level, cancellationToken);
        }
        catch
        {
            call.Value.Dispose();
            throw;
        }

        if (access.Error != null)
        {
            call.Value.Dispose();
            return GroupCallResolution.Failed(TranslateAccessError(access.Error));
        }

        return GroupCallResolution.Resolved(call.Value, access);
    }

    /// <summary>
    /// The conference gate. Read needs only the access hash, exactly as for a
    /// hosted call, because a client has to be able to look at a conference
    /// before it can join one. Anything beyond that needs a live participant row,
    /// and the manage level additionally needs the creator: TDLib's own rule for
    /// a call not bound to a chat is <c>groupCall.is_owned</c>.
    /// </summary>
    private async ValueTask<GroupCallPeerAccess> AuthorizeConferenceAsync(long authKeyId,
        long callId, long creatorUserId, GroupCallAccessLevel level,
        CancellationToken cancellationToken)
    {
        long currentUserId;
        using (TLDto.TLAuthInfo? auth = await _authorizationRepository
                   .GetAuthorizationAsync(authKeyId))
        {
            currentUserId = auth?.AsAuthInfo().UserId ?? 0;
        }
        if (currentUserId == 0)
        {
            return GroupCallPeerAccess.Failed(GroupCallErrors.AuthKeyInvalid);
        }

        bool isCreator = currentUserId == creatorUserId;
        if (level == GroupCallAccessLevel.Manage && !isCreator)
        {
            return GroupCallPeerAccess.Failed(GroupCallErrors.GroupCallForbidden);
        }
        // Ownership alone carries the manage level: the creator may end a call it
        // has already left, and a client that lost its row still has to be able to
        // stop the call rather than leave it running forever.
        if (level == GroupCallAccessLevel.Participate)
        {
            using TLDto.TLGroupCallParticipantState? participant = await _groupCallsRepository.GetParticipantAsync(callId, currentUserId,
                    cancellationToken);
            if (participant == null ||
                participant.Value.AsGroupCallParticipantState().Left)
            {
                return GroupCallPeerAccess.Failed(GroupCallErrors.GroupCallForbidden);
            }
        }

        return GroupCallPeerAccess.Conference(currentUserId, creatorUserId, isCreator);
    }

    protected async ValueTask<bool> IsActiveParticipantAsync(long callId, long userId,
        CancellationToken cancellationToken = default)
    {
        using TLDto.TLGroupCallParticipantState? participant = await _groupCallsRepository.GetParticipantAsync(callId, userId, cancellationToken);
        return participant != null &&
               !participant.Value.AsGroupCallParticipantState().Left;
    }

    /// <summary>
    /// Every account currently joined to a conference. This is the conference's
    /// whole notion of membership: there is no chat to walk, so fan-out and the
    /// participation gate both come from here. A caller that is about to clear the
    /// participant list has to read it FIRST — unlike a chat, the audience does
    /// not outlive the call.
    /// </summary>
    protected async ValueTask<List<long>> GetConferenceMemberIdsAsync(long callId,
        long? excludeUserId = null, CancellationToken cancellationToken = default)
    {
        var memberIds = new List<long>();
        string? offset = null;
        do
        {
            GroupCallParticipantPage page = await _groupCallsRepository
                .GetParticipantsPageAsync(callId, offset, ConferencePageSize,
                    cancellationToken);
            foreach (TLDto.TLGroupCallParticipantState participant in page.Participants)
            {
                using (participant)
                {
                    var view = participant.AsGroupCallParticipantState();
                    if (!view.Left && view.UserId != excludeUserId)
                    {
                        memberIds.Add(view.UserId);
                    }
                }
            }
            offset = page.NextOffset;
        } while (offset != null);

        return memberIds;
    }

    private const int ConferencePageSize = 200;

    /// <summary>
    /// Delivers one viewer-correct update to every joined participant except the
    /// invoker, whose copy travels in the RPC result. The builder returns null for
    /// a member that should not receive this update at all.
    /// </summary>
    protected async Task<int> PushToConferenceAsync(long callId, long? excludeUserId,
        Func<long, Task<TLUpdate?>> buildForMember) =>
        await Fanout.PushGroupCallUpdatesToAsync(
            await GetConferenceMemberIdsAsync(callId, excludeUserId), buildForMember);

    /// <summary>
    /// A conference's own updateGroupCall. chat_id is omitted because the call has
    /// no hosting dialog to attach itself to.
    /// </summary>
    protected byte[] BuildConferenceCallUpdateBytes(TLDto.TLGroupCallState state,
        GroupCallViewer viewer, int unmutedVideoCount)
    {
        using TLGroupCall call = GroupCallBuilders.BuildCall(state, viewer, VideoOptions,
            unmutedVideoCount);
        using TLUpdate update = UpdateGroupCall.Builder()
            .Call(call.AsSpan())
            .Build();
        return update.AsSpan().ToArray();
    }

    /// <summary>
    /// The invoker's Updates result for a conference transition. There is no
    /// hosting chat row to carry, so the chats vector is empty and only the
    /// accounts a carried row refers to are hydrated.
    /// </summary>
    protected async ValueTask<TLUpdates> BuildConferenceResultAsync(long authKeyId,
        long userId, IReadOnlyCollection<byte[]> updateBytes,
        IReadOnlyCollection<long>? extraUserIds = null)
    {
        int seq = await UpdatesContexts.GetUpdatesContext(authKeyId, userId)
            .IncrementSeq();
        var userIds = new List<long> { userId };
        if (extraUserIds != null)
        {
            userIds.AddRange(extraUserIds);
        }
        return Fanout.BuildUpdates(updateBytes, userIds, Array.Empty<byte[]>(), Now(),
            seq);
    }

    /// <summary>
    /// The unsequenced variant, for a result that carries the caller's own media
    /// credentials. See <see cref="BuildUnsequencedResultAsync"/> for why a join
    /// answer must leave the seq sequence.
    /// </summary>
    protected TLUpdates BuildUnsequencedConferenceResult(long userId,
        IReadOnlyCollection<byte[]> updateBytes,
        IReadOnlyCollection<long>? extraUserIds = null)
    {
        var userIds = new List<long> { userId };
        if (extraUserIds != null)
        {
            userIds.AddRange(extraUserIds);
        }
        return Fanout.BuildUpdates(updateBytes, userIds, Array.Empty<byte[]>(), Now(),
            seq: 0);
    }

    private static string TranslateAccessError(string error) => error switch
    {
        GroupCallErrors.UserNotParticipant => GroupCallErrors.GroupCallForbidden,
        GroupCallErrors.PeerIdInvalid => GroupCallErrors.GroupCallInvalid,
        _ => error,
    };

    /// <summary>
    /// The perspective one account sees a call from. The subscription flag is
    /// viewer-local state, so it is read per member and never copied from the
    /// canonical row.
    /// </summary>
    protected async ValueTask<GroupCallViewer> BuildViewerAsync(long callId, long userId,
        bool canManageCall, CancellationToken cancellationToken = default)
    {
        using TLDto.TLGroupCallViewerState? viewerState = await _groupCallsRepository.GetViewerStateAsync(callId, userId, cancellationToken);
        bool subscribed = viewerState != null &&
                          viewerState.Value.AsGroupCallViewerState().ScheduleStartSubscribed;
        return new GroupCallViewer(userId, canManageCall, subscribed);
    }

    /// <summary>
    /// updateGroupCall carries chat_id so the client can attach the call to the
    /// hosting dialog without a full-chat refetch.
    /// </summary>
    protected byte[] BuildCallUpdateBytes(TLDto.TLGroupCallState state,
        GroupCallViewer viewer, long chatId, int unmutedVideoCount)
    {
        using TLGroupCall call = GroupCallBuilders.BuildCall(state, viewer,
            VideoOptions, unmutedVideoCount);
        using TLUpdate update = UpdateGroupCall.Builder()
            .ChatId(chatId)
            .Call(call.AsSpan())
            .Build();
        return update.AsSpan().ToArray();
    }

    /// <summary>
    /// Delivers one viewer-correct updateGroupCall to every active member except
    /// the invoker, whose copy travels in the RPC result instead.
    /// </summary>
    protected Task<int> PushCallUpdateToOtherMembersAsync(TLDto.TLGroupCallState state,
        long peerChatId, long invokerUserId, int unmutedVideoCount)
    {
        // The view is a ref struct, so the call id is read out here rather than
        // inside the async per-member builder.
        long callId = state.AsGroupCallState().Id;
        return Fanout.PushGroupCallUpdatesAsync(peerChatId, invokerUserId, async memberId =>
        {
            bool canManage = await CanManageCallAsync(peerChatId, memberId);
            GroupCallViewer viewer = await BuildViewerAsync(callId, memberId, canManage);
            using TLGroupCall call = GroupCallBuilders.BuildCall(state, viewer,
                VideoOptions, unmutedVideoCount);
            return UpdateGroupCall.Builder()
                .ChatId(peerChatId)
                .Call(call.AsSpan())
                .Build();
        });
    }

    /// <summary>
    /// How many participants are currently joined with video, which is what
    /// <c>groupCall.unmuted_video_count</c> reports and what
    /// <c>can_start_video</c> is gated against. A discarded call keeps no
    /// participant rows, so it never needs the read.
    /// </summary>
    protected ValueTask<int> CountUnmutedVideoAsync(long callId, bool discarded = false) =>
        discarded
            ? ValueTask.FromResult(0)
            : _groupCallsRepository.CountActiveVideoParticipantsAsync(callId);

    /// <summary>
    /// One participant row's overlay as a single viewer sees it. The media plane
    /// rewrites SSRCs per consumer, so the audio source and the camera/screen
    /// source groups come from that viewer's own mapping; a viewer with no
    /// mapping falls back to the canonical source and gets no video rows at all.
    /// </summary>
    protected GroupCallParticipantOverlay BuildOverlay(long callId, string? viewerMediaId,
        string? producerMediaId, bool mutedByYou = false, int? localVolume = null)
    {
        GroupCallViewerSources? sources = SourceMap.TryGet(callId, viewerMediaId,
            producerMediaId);
        return new GroupCallParticipantOverlay(mutedByYou, localVolume,
            sources?.AudioSource ?? 0, sources);
    }

    /// <summary>
    /// updateGroupCallParticipants for a single already-built participant row.
    /// The version is the call's version AFTER the mutation that produced the
    /// row, so a client can apply it as a versioned step.
    /// </summary>
    protected static TLUpdate BuildParticipantsUpdate(TLDto.TLGroupCallState state,
        ReadOnlySpan<byte> participantRow)
    {
        using TLInputGroupCall inputCall = GroupCallBuilders.BuildInputGroupCall(state);
        var participants = new Vector();
        participants.AppendTLObject(participantRow);
        return UpdateGroupCallParticipants.Builder()
            .Call(inputCall.AsSpan())
            .Participants(participants)
            .Version(state.AsGroupCallState().Version)
            .Build();
    }

    /// <summary>
    /// One member's full overlay for one participant row: its STORED local mute
    /// and volume for that participant merged with its media mapping. Fan-out
    /// rows are non-min, so pinned TDLib overwrites its local mute/volume with
    /// whatever the row carries — omitting the stored values here would silently
    /// reset every receiver's local state on each versioned update.
    /// </summary>
    protected async ValueTask<GroupCallParticipantOverlay> BuildMemberOverlayAsync(
        long callId, long viewerUserId, string? viewerMediaId, long targetUserId,
        string? producerMediaId)
    {
        bool mutedByYou = false;
        int? localVolume = null;
        using (TLDto.TLGroupCallViewerParticipantState? local = await _groupCallsRepository.GetViewerParticipantStateAsync(callId,
                       viewerUserId, targetUserId))
        {
            if (local != null)
            {
                var view = local.Value.AsGroupCallViewerParticipantState();
                mutedByYou = view.MutedByYou;
                localVolume = view.Flags[1] ? view.Volume : null;
            }
        }

        return BuildOverlay(callId, viewerMediaId, producerMediaId, mutedByYou,
            localVolume);
    }

    /// <summary>
    /// The stored media correlation id for one participant, or null when the
    /// account has no row in the call.
    /// </summary>
    protected async ValueTask<string?> GetMediaIdAsync(long callId, long userId,
        CancellationToken cancellationToken = default)
    {
        using TLDto.TLGroupCallParticipantState? participant = await _groupCallsRepository.GetParticipantAsync(callId, userId, cancellationToken);
        if (participant == null)
        {
            return null;
        }

        var view = participant.Value.AsGroupCallParticipantState();
        return view.Left ? null : Encoding.UTF8.GetString(view.MediaId);
    }

    /// <summary>
    /// The invoker's Updates result for a call transition that writes no message
    /// into the hosting box: the viewer's own call updates plus the hosting chat
    /// row, under a fresh per-auth-key seq. <paramref name="extraUserIds"/> names
    /// additional accounts a carried row refers to (the target of a moderation
    /// edit) so the users vector hydrates them.
    /// </summary>
    protected async ValueTask<TLUpdates> BuildInvokerResultAsync(long authKeyId,
        long userId, IReadOnlyCollection<byte[]> updateBytes, byte[] chatBytes,
        IReadOnlyCollection<long>? extraUserIds = null)
    {
        int seq = await UpdatesContexts.GetUpdatesContext(authKeyId, userId)
            .IncrementSeq();
        var userIds = new List<long> { userId };
        if (extraUserIds != null)
        {
            userIds.AddRange(extraUserIds);
        }
        return Fanout.BuildUpdates(updateBytes, userIds, new[] { chatBytes },
            Now(), seq);
    }

    /// <summary>
    /// The invoker's Updates result for a join answer, carried OUTSIDE the seq
    /// sequence (seq 0).
    ///
    /// A sequenced container is only applied when its seq continues the client's
    /// own sequence: pinned TDLib postpones one that runs ahead and silently drops
    /// one that repeats a seq it already has (UpdatesManager.cpp:2658-2677). An RPC
    /// result travels on the request channel while fan-out travels through the
    /// update box, so a join answer can legitimately overtake the updates before
    /// it — and a postponed join answer never resolves the client's join promise,
    /// surfacing as "Wrong join response received" rather than as an error from
    /// here. seq 0 means "apply unconditionally", which is what a result carrying
    /// the caller's own media credentials has to do.
    /// </summary>
    protected ValueTask<TLUpdates> BuildUnsequencedResultAsync(long userId,
        IReadOnlyCollection<byte[]> updateBytes, byte[] chatBytes) =>
        ValueTask.FromResult(Fanout.BuildUpdates(updateBytes, new[] { userId },
            new[] { chatBytes }, Now(), seq: 0));

    /// <summary>
    /// can_change_join_muted is per viewer, so each recipient's manage right is
    /// read from its own participant row.
    /// </summary>
    protected async ValueTask<bool> CanManageCallAsync(long chatId, long userId)
    {
        using TLDto.TLChatParticipantInfo? participant = await _chatParticipantsRepository.GetParticipantAsync(chatId, userId);
        return participant != null && ChatRights.HasAdminRight(participant.Value,
            ChatAdminRightRequirement.ManageCall);
    }
}
