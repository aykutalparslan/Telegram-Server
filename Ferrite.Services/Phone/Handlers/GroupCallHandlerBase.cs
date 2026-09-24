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
    private readonly IMessageRepository _messageRepository;

    protected readonly IUnitOfWork UnitOfWork;
    protected readonly UpdateFanout Fanout;
    protected readonly GroupCallChatLink ChatLink;
    protected readonly IUpdatesContextFactory UpdatesContexts;
    protected readonly IMTProtoTime Time;
    protected readonly GroupCallVideoOptions VideoOptions;
    protected readonly GroupCallMediaSourceMap SourceMap;
    protected readonly ILogger Log;

    protected GroupCallHandlerBase(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IAuthorizationRepository authorizationRepository, IGroupCallsRepository groupCallsRepository, IMessageRepository messageRepository, UpdateFanout fanout,
        GroupCallChatLink chatLink, IUpdatesContextFactory updatesContexts,
        IMTProtoTime time, GroupCallVideoOptions videoOptions,
        GroupCallMediaSourceMap sourceMap, ILogger log)
    {
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;

        _authorizationRepository = authorizationRepository;
        _groupCallsRepository = groupCallsRepository;
        _messageRepository = messageRepository;

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
        out long accessHash, out string? slug, out int inviteMsgId)
    {
        if (view.Is(out InputGroupCall call) && call.Id != 0)
        {
            id = call.Id;
            accessHash = call.AccessHash;
            slug = null;
            inviteMsgId = 0;
            return true;
        }

        id = 0;
        accessHash = 0;
        slug = null;
        inviteMsgId = 0;
        if (view.Is(out InputGroupCallSlug bySlug) && !bySlug.Slug.IsEmpty)
        {
            slug = Encoding.UTF8.GetString(bySlug.Slug);
        }
        else if (view.Is(out InputGroupCallInviteMessage invite) && invite.MsgId != 0)
        {
            inviteMsgId = invite.MsgId;
        }

        return false;
    }

    protected async ValueTask<(bool Read, long Id, long AccessHash)> ResolveCallSlugAsync(
        string slug, CancellationToken cancellationToken = default)
    {
        using TLDto.TLGroupCallInvite? invite = await _groupCallsRepository
            .GetInviteByHashAsync(slug, cancellationToken);
        if (invite == null)
        {
            return (false, 0, 0);
        }

        var inviteView = invite.Value.AsGroupCallInvite();
        if (inviteView.Revoked)
        {
            return (false, 0, 0);
        }
        if (inviteView.Flags[2] && inviteView.ExpiryDate <= Now())
        {
            return (false, 0, 0);
        }

        long slugCallId = inviteView.CallId;
        using TLDto.TLGroupCallState? slugCall = await _groupCallsRepository
            .GetCallAsync(slugCallId, cancellationToken);
        if (slugCall == null)
        {
            return (false, 0, 0);
        }

        return (true, slugCallId, slugCall.Value.AsGroupCallState().AccessHash);
    }

    protected async ValueTask<(bool Read, long Id, long AccessHash)> ResolveCallAddressAsync(
        long authKeyId, string? slug, int inviteMsgId,
        CancellationToken cancellationToken = default)
    {
        if (slug != null)
        {
            return await ResolveCallSlugAsync(slug, cancellationToken);
        }
        if (inviteMsgId == 0)
        {
            return (false, 0, 0);
        }

        long invitedUserId = await ResolveUserIdAsync(authKeyId);
        if (invitedUserId == 0)
        {
            return (false, 0, 0);
        }

        long invitedCallId = await ReadInvitedCallIdAsync(invitedUserId, inviteMsgId);
        if (invitedCallId == 0)
        {
            return (false, 0, 0);
        }

        using TLDto.TLGroupCallState? invitedCall = await _groupCallsRepository
            .GetCallAsync(invitedCallId, cancellationToken);
        if (invitedCall == null)
        {
            return (false, 0, 0);
        }

        return (true, invitedCallId, invitedCall.Value.AsGroupCallState().AccessHash);
    }

    protected async ValueTask<long> ResolveUserIdAsync(long authKeyId)
    {
        using TLDto.TLAuthInfo? auth = await _authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        return auth?.AsAuthInfo().UserId ?? 0;
    }

    protected async ValueTask<long> ReadInvitedCallIdAsync(long userId, int msgId)
    {
        if (msgId == 0)
        {
            return 0;
        }

        using TLDto.TLSavedMessage? saved = await _messageRepository
            .GetMessageAsync(userId, msgId);
        if (saved == null)
        {
            return 0;
        }

        TLMessage message = saved.Value.AsSavedMessage().Get_OriginalMessage();
        if (message.Type != TLMessage.MessageType.MessageService)
        {
            return 0;
        }

        var action = new MessageActionView(message.AsMessageService().Action);
        return action.Is(out MessageActionConferenceCall conference)
            ? conference.CallId
            : 0;
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
        var userIds = new List<long> { userId };
        if (extraUserIds != null)
        {
            userIds.AddRange(extraUserIds);
        }
        return Fanout.BuildUpdates(userId, updateBytes, userIds, Array.Empty<byte[]>(), Now(),
            seq: 0);
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
        using TLPeer? peer = BuildGroupCallPeer(state);
        var updateBuilder = UpdateGroupCall.Builder().Call(call.AsSpan());
        if (peer != null) updateBuilder = updateBuilder.Peer(peer.Value.AsSpan());
        using TLUpdate update = updateBuilder.Build();
        return update.AsSpan().ToArray();
    }

    protected Task<int> PushCallUpdateToOtherMembersAsync(TLDto.TLGroupCallState state,
        GroupCallPeerAccess access, long invokerUserId, int unmutedVideoCount)
    {
        long callId = state.AsGroupCallState().Id;
        return PushToCallMembersAsync(access, callId, invokerUserId, async memberId =>
        {
            bool canManage = await CanManageCallAsync(access, memberId);
            GroupCallViewer viewer = await BuildViewerAsync(callId, memberId, canManage);
            using TLGroupCall call = GroupCallBuilders.BuildCall(state, viewer,
                VideoOptions, unmutedVideoCount);
            using TLPeer? peer = BuildGroupCallPeer(state);
            var updateBuilder = UpdateGroupCall.Builder().Call(call.AsSpan());
            if (peer != null) updateBuilder = updateBuilder.Peer(peer.Value.AsSpan());
            return updateBuilder.Build();
        });
    }

    protected async Task<int> PushToCallMembersAsync(GroupCallPeerAccess access,
        long callId, long? excludeUserId, Func<long, Task<TLUpdate?>> buildForMember) =>
        access.IsConference
            ? await PushToConferenceAsync(callId, excludeUserId, buildForMember)
            : await Fanout.PushGroupCallUpdatesAsync(access.Peer.Id, excludeUserId,
                buildForMember);

    protected async ValueTask<bool> CanManageCallAsync(GroupCallPeerAccess access,
        long userId) =>
        access.IsConference
            ? userId == access.Peer.Id
            : await CanManageCallAsync(access.Peer.Id, userId);

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
            using TLPeer? peer = BuildGroupCallPeer(state);
            var updateBuilder = UpdateGroupCall.Builder().Call(call.AsSpan());
            if (peer != null) updateBuilder = updateBuilder.Peer(peer.Value.AsSpan());
            return updateBuilder.Build();
        });
    }

    private static TLPeer? BuildGroupCallPeer(TLDto.TLGroupCallState state)
    {
        var view = state.AsGroupCallState();
        return (GroupCallPeerType)view.PeerType switch
        {
            GroupCallPeerType.Chat => PeerChat.Builder().ChatId(view.PeerId).Build(),
            GroupCallPeerType.Channel => PeerChannel.Builder().ChannelId(view.PeerId).Build(),
            _ => null,
        };
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
        long userId, IReadOnlyCollection<byte[]> updateBytes, byte[]? chatBytes,
        IReadOnlyCollection<long>? extraUserIds = null)
    {
        var userIds = new List<long> { userId };
        if (extraUserIds != null)
        {
            userIds.AddRange(extraUserIds);
        }
        return Fanout.BuildUpdates(userId, updateBytes, userIds, ChatRows(chatBytes),
            Now(), seq: 0);
    }

    protected ValueTask<TLUpdates> BuildUnsequencedResultAsync(long userId,
        IReadOnlyCollection<byte[]> updateBytes, byte[]? chatBytes) =>
        ValueTask.FromResult(Fanout.BuildUpdates(userId, updateBytes, new[] { userId },
            ChatRows(chatBytes), Now(), seq: 0));

    private static byte[][] ChatRows(byte[]? chatBytes) =>
        chatBytes == null ? Array.Empty<byte[]>() : new[] { chatBytes };

    protected async ValueTask<bool> CanManageCallAsync(long chatId, long userId)
    {
        using TLDto.TLChatParticipantInfo? participant = await _chatParticipantsRepository.GetParticipantAsync(chatId, userId);
        return participant != null && ChatRights.HasAdminRight(participant.Value,
            ChatAdminRightRequirement.ManageCall);
    }
}
