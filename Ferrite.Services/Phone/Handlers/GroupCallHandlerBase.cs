// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.Services.Calls;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.Utils;
using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Phone.Handlers;

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

    protected async Task<int> PushToConferenceAsync(long callId, long? excludeUserId,
        Func<long, Task<TLUpdate?>> buildForMember) =>
        await Fanout.PushGroupCallUpdatesToAsync(
            await GetConferenceMemberIdsAsync(callId, excludeUserId), buildForMember);

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
        return Fanout.BuildUpdates(userId, updateBytes, userIds, Array.Empty<byte[]>(), Now(),
            seq);
    }

    protected TLUpdates BuildUnsequencedConferenceResult(long userId,
        IReadOnlyCollection<byte[]> updateBytes,
        IReadOnlyCollection<long>? extraUserIds = null)
    {
        var userIds = new List<long> { userId };
        if (extraUserIds != null)
        {
            userIds.AddRange(extraUserIds);
        }
        return Fanout.BuildUpdates(userId, updateBytes, userIds, Array.Empty<byte[]>(), Now(),
            seq: 0);
    }

    private static string TranslateAccessError(string error) => error switch
    {
        GroupCallErrors.UserNotParticipant => GroupCallErrors.GroupCallForbidden,
        GroupCallErrors.PeerIdInvalid => GroupCallErrors.GroupCallInvalid,
        _ => error,
    };

    protected async ValueTask<GroupCallViewer> BuildViewerAsync(long callId, long userId,
        bool canManageCall, CancellationToken cancellationToken = default)
    {
        using TLDto.TLGroupCallViewerState? viewerState = await _groupCallsRepository.GetViewerStateAsync(callId, userId, cancellationToken);
        bool subscribed = viewerState != null &&
                          viewerState.Value.AsGroupCallViewerState().ScheduleStartSubscribed;
        return new GroupCallViewer(userId, canManageCall, subscribed);
    }

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

    protected Task<int> PushCallUpdateToOtherMembersAsync(TLDto.TLGroupCallState state,
        long peerChatId, long invokerUserId, int unmutedVideoCount)
    {
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

    protected ValueTask<int> CountUnmutedVideoAsync(long callId, bool discarded = false) =>
        discarded
            ? ValueTask.FromResult(0)
            : _groupCallsRepository.CountActiveVideoParticipantsAsync(callId);

    protected GroupCallParticipantOverlay BuildOverlay(long callId, string? viewerMediaId,
        string? producerMediaId, bool mutedByYou = false, int? localVolume = null)
    {
        GroupCallViewerSources? sources = SourceMap.TryGet(callId, viewerMediaId,
            producerMediaId);
        return new GroupCallParticipantOverlay(mutedByYou, localVolume,
            sources?.AudioSource ?? 0, sources);
    }

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
        return Fanout.BuildUpdates(userId, updateBytes, userIds, new[] { chatBytes },
            Now(), seq);
    }

    protected ValueTask<TLUpdates> BuildUnsequencedResultAsync(long userId,
        IReadOnlyCollection<byte[]> updateBytes, byte[] chatBytes) =>
        ValueTask.FromResult(Fanout.BuildUpdates(userId, updateBytes, new[] { userId },
            new[] { chatBytes }, Now(), seq: 0));

    protected async ValueTask<bool> CanManageCallAsync(long chatId, long userId)
    {
        using TLDto.TLChatParticipantInfo? participant = await _chatParticipantsRepository.GetParticipantAsync(chatId, userId);
        return participant != null && ChatRights.HasAdminRight(participant.Value,
            ChatAdminRightRequirement.ManageCall);
    }
}
