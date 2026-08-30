// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.MessageMethods;

public abstract class ChatJoinRequestHandlerBase
{
    private readonly IChannelMessagesRepository _channelMessagesRepository;

    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatInvitesRepository _chatInvitesRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IUserRepository _userRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICounterFactory _counterFactory;
    private readonly IUpdatesContextFactory _updatesContextFactory;
    private readonly ChatRowStore _chatRows;
    private readonly InviteStore _invites;
    private readonly MessageStore _messages;
    private readonly UpdateFanout _fanout;
    private readonly TimeProvider _timeProvider;

    protected ChatJoinRequestHandlerBase(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IAuthorizationRepository authorizationRepository, IChatInvitesRepository chatInvitesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IUserRepository userRepository,
        ICounterFactory counterFactory, IUpdatesContextFactory updatesContextFactory,
        ChatRowStore chatRows, InviteStore invites, MessageStore messages,
        UpdateFanout fanout, TimeProvider timeProvider)
    {
        _channelMessagesRepository = channelMessagesRepository;

        _authorizationRepository = authorizationRepository;
        _chatInvitesRepository = chatInvitesRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;
        _userRepository = userRepository;

        _unitOfWork = unitOfWork;
        _counterFactory = counterFactory;
        _updatesContextFactory = updatesContextFactory;
        _chatRows = chatRows;
        _invites = invites;
        _messages = messages;
        _fanout = fanout;
        _timeProvider = timeProvider;
    }

    protected readonly record struct InvitePeerSelection(bool Valid, bool IsChannel,
        long ChatId);

    protected readonly record struct InviteUserSelection(bool Valid, bool IsSelf,
        long UserId);

    private sealed record ParticipantSnapshot(long UserId, int Role, long InviterId,
        int Date);

    private sealed record ChannelServiceWrite(byte[] MessageBytes, int Pts);

    protected static InvitePeerSelection ReadInvitePeer(InputPeerView peer)
    {
        if (peer.Is(out InputPeerChat chat))
        {
            return new InvitePeerSelection(true, false, chat.ChatId);
        }
        if (peer.Is(out InputPeerChannel channel))
        {
            return new InvitePeerSelection(true, true, channel.ChannelId);
        }
        if (peer.Is(out InputPeerChannelFromMessage fromMessage))
        {
            return new InvitePeerSelection(true, true, fromMessage.ChannelId);
        }

        return default;
    }

    protected static InviteUserSelection ReadInviteUser(InputUserView user)
    {
        if (user.Is(out InputUserSelf _))
        {
            return new InviteUserSelection(true, true, 0);
        }
        if (user.Is(out InputUser inputUser))
        {
            return new InviteUserSelection(true, false, inputUser.UserId);
        }
        if (user.Is(out InputUserFromMessage fromMessage))
        {
            return new InviteUserSelection(true, false, fromMessage.UserId);
        }

        return default;
    }

    protected Task<TLUpdates> HandleSingleAsync(long authKeyId,
        InvitePeerSelection peer, InviteUserSelection user, bool approved) =>
        HandleAsync(authKeyId, peer, approved, user, link: null, all: false);

    protected Task<TLUpdates> HandleAllAsync(long authKeyId,
        InvitePeerSelection peer, bool approved, string? link) =>
        HandleAsync(authKeyId, peer, approved, default, link, all: true);

    private async Task<TLUpdates> HandleAsync(long authKeyId,
        InvitePeerSelection peer, bool approved, InviteUserSelection requestedUser,
        string? link, bool all)
    {
        using TLAuthInfo? authorization = await _authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        if (authorization == null || !authorization.Value.AsAuthInfo().LoggedIn)
        {
            return ErrorUpdates("AUTH_KEY_INVALID");
        }

        long currentUserId = authorization.Value.AsAuthInfo().UserId;
        if (!peer.Valid || peer.ChatId <= 0)
        {
            return ErrorUpdates("PEER_ID_INVALID");
        }

        byte[] chatBytes;
        bool megagroup = false;
        using (TLChat? storedChat = await _chatRepository
                   .GetChatAsync(peer.ChatId))
        {
            if (peer.IsChannel)
            {
                if (storedChat == null || storedChat.Value.Type != TLChat.ChatType.Channel)
                {
                    return ErrorUpdates("CHANNEL_INVALID");
                }

                var channel = storedChat.Value.AsChannel();
                megagroup = channel.Megagroup;
            }
            else if (storedChat == null || storedChat.Value.Type != TLChat.ChatType.Chat ||
                     storedChat.Value.AsChat().Deactivated)
            {
                return ErrorUpdates("CHAT_ID_INVALID");
            }

            chatBytes = storedChat.Value.AsSpan().ToArray();
        }

        IReadOnlyCollection<TLChatParticipantInfo> participantRows = await _chatParticipantsRepository.GetParticipantsAsync(peer.ChatId);
        var participants = new List<ParticipantSnapshot>(participantRows.Count);
        var activeIds = new HashSet<long>();
        var bannedIds = new HashSet<long>();
        var adminIds = new HashSet<long>();
        bool callerFound = false;
        bool callerCanInvite = false;
        foreach (TLChatParticipantInfo participantRow in participantRows)
        {
            using (participantRow)
            {
                var info = participantRow.AsChatParticipantInfo();
                if (!IsActiveParticipant(info.Role))
                {
                    if (info.Role == (int)ChatParticipantRole.Banned)
                    {
                        bannedIds.Add(info.UserId);
                    }
                    continue;
                }

                participants.Add(new ParticipantSnapshot(info.UserId, info.Role,
                    info.InviterId, info.Date));
                activeIds.Add(info.UserId);
                bool canInvite = peer.IsChannel
                    ? ChatRights.HasAdminRight(participantRow,
                        ChatAdminRightRequirement.InviteUsers)
                    : info.Role is (int)ChatParticipantRole.Creator or
                        (int)ChatParticipantRole.Admin;
                if (canInvite)
                {
                    adminIds.Add(info.UserId);
                }
                if (info.UserId == currentUserId)
                {
                    callerFound = true;
                    callerCanInvite = canInvite;
                }
            }
        }

        if (!callerFound)
        {
            return ErrorUpdates("USER_NOT_PARTICIPANT");
        }
        if (!callerCanInvite)
        {
            return ErrorUpdates("CHAT_ADMIN_REQUIRED");
        }

        long requestedUserId = 0;
        if (!all)
        {
            requestedUserId = requestedUser.IsSelf
                ? currentUserId
                : requestedUser.UserId;
            if (!requestedUser.Valid || requestedUserId <= 0)
            {
                return ErrorUpdates("USER_ID_INVALID");
            }
            using TLUser? user = _userRepository.GetUser(requestedUserId);
            if (user == null)
            {
                return ErrorUpdates("USER_ID_INVALID");
            }
        }

        List<PendingInviteImporter> pending = await ChatInvites.GetPendingImportersAsync(
            _chatInvitesRepository, peer.ChatId);
        List<PendingInviteImporter> selected;
        if (all)
        {
            string? requestedHash = link == null ? null : ChatInvites.HashFromLink(link);
            selected = pending
                .Where(x => requestedHash == null ||
                            ChatInvites.HashFromLink(x.Link) == requestedHash)
                .OrderBy(x => x.Date)
                .ThenBy(x => x.UserId)
                .ToList();
        }
        else
        {
            selected = pending.Where(x => x.UserId == requestedUserId).Take(1).ToList();
            if (selected.Count == 0)
            {
                return ErrorUpdates("USER_ID_INVALID");
            }
            if (approved && bannedIds.Contains(requestedUserId))
            {
                return ErrorUpdates("USER_BANNED_IN_CHANNEL");
            }
        }

        var selectedIds = selected.Select(x => x.UserId).ToHashSet();
        List<PendingInviteImporter> remaining = pending
            .Where(x => !selectedIds.Contains(x.UserId))
            .ToList();
        List<StoredInvite> storedInvites = selected.Count == 0
            ? new List<StoredInvite>()
            : await _invites.GetStoredInvitesAsync(peer.ChatId);
        Dictionary<string, StoredInvite> invitesByHash = storedInvites
            .GroupBy(x => x.Hash)
            .ToDictionary(x => x.Key, x => x.First());

        var validUsers = new HashSet<long>();
        if (approved)
        {
            foreach (PendingInviteImporter importer in selected)
            {
                using TLUser? user = _userRepository.GetUser(importer.UserId);
                if (user != null && !bannedIds.Contains(importer.UserId))
                {
                    validUsers.Add(importer.UserId);
                }
                else if (!all)
                {
                    return ErrorUpdates("USER_ID_INVALID");
                }
            }
        }

        int date = (int)_timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var added = new List<PendingInviteImporter>();
        foreach (PendingInviteImporter importer in selected)
        {
            _chatInvitesRepository.DeletePendingImporter(peer.ChatId,
                importer.UserId);
            if (!approved || !validUsers.Contains(importer.UserId) ||
                activeIds.Contains(importer.UserId))
            {
                continue;
            }

            using (TLChatParticipantInfo participant = ChatParticipantInfo.Builder()
                       .ChatId(peer.ChatId)
                       .UserId(importer.UserId)
                       .Role((int)ChatParticipantRole.Member)
                       .InviterId(currentUserId)
                       .Date(date)
                       .Build())
            {
                _chatParticipantsRepository.PutParticipant(participant);
            }
            using (TLChatInviteImporterInfo storedImporter = ChatInviteImporterInfo.Builder()
                       .ChatId(peer.ChatId)
                       .UserId(importer.UserId)
                       .Date(date)
                       .Link(Encoding.UTF8.GetBytes(importer.Link))
                       .Build())
            {
                _chatInvitesRepository.PutImporter(storedImporter);
            }

            participants.Add(new ParticipantSnapshot(importer.UserId,
                (int)ChatParticipantRole.Member, currentUserId, date));
            activeIds.Add(importer.UserId);
            added.Add(importer);
        }

        Dictionary<string, int> selectedByHash = selected
            .GroupBy(x => ChatInvites.HashFromLink(x.Link))
            .ToDictionary(x => x.Key, x => x.Count());
        Dictionary<string, int> addedByHash = added
            .GroupBy(x => ChatInvites.HashFromLink(x.Link))
            .ToDictionary(x => x.Key, x => x.Count());
        foreach ((string hash, int selectedCount) in selectedByHash)
        {
            if (!invitesByHash.TryGetValue(hash, out StoredInvite? invite))
            {
                continue;
            }

            int addedCount = addedByHash.GetValueOrDefault(hash);
            int usage = invite.Usage > int.MaxValue - addedCount
                ? int.MaxValue
                : invite.Usage + addedCount;
            _invites.PutStoredInvite(invite, invite.Revoked, invite.RequestNeeded,
                invite.ExpireDate, invite.UsageLimit, usage, invite.Title,
                requested: Math.Max(0, invite.Requested - selectedCount));
        }

        byte[] resultChatBytes = chatBytes;
        byte[]? membershipUpdateBytes = null;
        var channelMembershipUpdates = new List<byte[]>();
        if (added.Count > 0)
        {
            if (peer.IsChannel)
            {
                resultChatBytes = _chatRows.UpdateStoredChannelParticipantsCount(chatBytes,
                    added.Count);
                foreach (PendingInviteImporter importer in added)
                {
                    string hash = ChatInvites.HashFromLink(importer.Link);
                    invitesByHash.TryGetValue(hash, out StoredInvite? invite);
                    channelMembershipUpdates.Add(BuildChannelParticipantUpdateBytes(
                        peer.ChatId, currentUserId, importer.UserId, date, invite));
                }
            }
            else
            {
                resultChatBytes = _chatRows.UpdateStoredChatMembership(chatBytes, added.Count);
                membershipUpdateBytes = BuildChatParticipantsUpdateBytes(peer.ChatId,
                    participants, ReadChatVersion(resultChatBytes));
            }
        }

        byte[] pendingUpdateBytes = BuildPendingUpdateBytes(peer, remaining);
        byte[] actionBytes;
        using (TLMessageAction action = MessageActionChatJoinedByRequest.Builder().Build())
        {
            actionBytes = action.AsSpan().ToArray();
        }

        var resultUpdateBytes = new List<byte[]>();
        var basicLiveWrites = new List<(long UserId, byte[] UpdateBytes)>();
        var channelServiceWrites = new List<ChannelServiceWrite>();
        if (!peer.IsChannel)
        {
            foreach (PendingInviteImporter importer in added)
            {
                foreach (long participantId in activeIds)
                {
                    StoredMessageWrite write = await _messages
                        .PutBasicGroupServiceMessageAsync(participantId, null, peer.ChatId,
                            importer.UserId, actionBytes, date);
                    byte[] updateBytes = BuildNewMessageUpdateBytes(write, channel: false);
                    basicLiveWrites.Add((participantId, updateBytes));
                    if (participantId == currentUserId)
                    {
                        resultUpdateBytes.Add(updateBytes);
                    }
                }
            }
            if (membershipUpdateBytes != null)
            {
                resultUpdateBytes.Add(membershipUpdateBytes);
            }
        }
        else
        {
            if (added.Count > 0)
            {
                using TLUpdate updateChannel = UpdateChannel.Builder()
                    .ChannelId(peer.ChatId)
                    .Build();
                resultUpdateBytes.Add(updateChannel.AsSpan().ToArray());
                resultUpdateBytes.AddRange(channelMembershipUpdates);
            }
            if (megagroup)
            {
                foreach (PendingInviteImporter importer in added)
                {
                    StoredMessageWrite write = await MessageStore.PutChannelServiceMessageAsync(_channelMessagesRepository, _counterFactory,
                            peer.ChatId, importer.UserId, actionBytes, date);
                    channelServiceWrites.Add(new ChannelServiceWrite(write.Bytes, write.Pts));
                    resultUpdateBytes.Add(BuildNewMessageUpdateBytes(write, channel: true));
                }
            }
        }
        resultUpdateBytes.Add(pendingUpdateBytes);

        if (!await _unitOfWork.SaveAsync())
        {
            return ErrorUpdates("INTERNAL_SERVER_ERROR");
        }

        foreach ((long participantId, byte[] updateBytes) in basicLiveWrites)
        {
            await _fanout.EnqueueSerializedAsync(participantId, updateBytes);
        }
        if (membershipUpdateBytes != null)
        {
            await _fanout.EnqueueSerializedAsync(activeIds,
                new[] { membershipUpdateBytes });
        }
        if (peer.IsChannel && added.Count > 0)
        {
            var membershipFanout = new List<byte[]>(channelMembershipUpdates.Count + 1);
            using (TLUpdate updateChannel = UpdateChannel.Builder()
                       .ChannelId(peer.ChatId)
                       .Build())
            {
                membershipFanout.Add(updateChannel.AsSpan().ToArray());
            }
            membershipFanout.AddRange(channelMembershipUpdates);
            await _fanout.EnqueueSerializedAsync(activeIds, membershipFanout);
            foreach (ChannelServiceWrite write in channelServiceWrites)
            {
                foreach (long participantId in activeIds)
                {
                    await _fanout.EnqueueNewChannelMessageAsync(participantId,
                        write.MessageBytes, write.Pts);
                }
            }
        }
        await _fanout.EnqueueSerializedAsync(adminIds, new[] { pendingUpdateBytes });

        int seq = peer.IsChannel
            ? await _updatesContextFactory.GetUpdatesContext(authKeyId, currentUserId)
                .IncrementSeq()
            : 0;
        var resultUserIds = new HashSet<long>(activeIds);
        foreach (PendingInviteImporter importer in remaining.OrderByDescending(x => x.Date)
                     .ThenBy(x => x.UserId).Take(3))
        {
            resultUserIds.Add(importer.UserId);
        }
        return _fanout.BuildUpdates(currentUserId, resultUpdateBytes, resultUserIds,
            new[] { resultChatBytes }, date, seq);
    }

    private static bool IsActiveParticipant(int role) =>
        role is not ((int)ChatParticipantRole.Banned) and not
            ((int)ChatParticipantRole.Left);

    private static int ReadChatVersion(byte[] chatBytes)
    {
        using var chat = new TLChat(chatBytes, 0, chatBytes.Length);
        return chat.AsChat().Version;
    }

    private static byte[] BuildChatParticipantsUpdateBytes(long chatId,
        IReadOnlyCollection<ParticipantSnapshot> participants, int version)
    {
        var participantVector = new Vector();
        foreach (ParticipantSnapshot snapshot in participants)
        {
            using TLChatParticipant participant = snapshot.Role switch
            {
                (int)ChatParticipantRole.Creator => ChatParticipantCreator.Builder()
                    .UserId(snapshot.UserId)
                    .Build(),
                (int)ChatParticipantRole.Admin => ChatParticipantAdmin.Builder()
                    .UserId(snapshot.UserId)
                    .InviterId(snapshot.InviterId)
                    .Date(snapshot.Date)
                    .Build(),
                _ => ChatParticipant.Builder()
                    .UserId(snapshot.UserId)
                    .InviterId(snapshot.InviterId)
                    .Date(snapshot.Date)
                    .Build()
            };
            participantVector.AppendTLObject(participant.AsSpan());
        }

        using TLChatParticipants chatParticipants = ChatParticipants.Builder()
            .ChatId(chatId)
            .Participants(participantVector)
            .Version(version)
            .Build();
        using TLUpdate update = UpdateChatParticipants.Builder()
            .Participants(chatParticipants.AsSpan())
            .Build();
        return update.AsSpan().ToArray();
    }

    private static byte[] BuildChannelParticipantUpdateBytes(long channelId,
        long actorUserId, long joinedUserId, int date, StoredInvite? invite)
    {
        using TLChannelParticipant participant = ChannelParticipant.Builder()
            .UserId(joinedUserId)
            .Date(date)
            .Build();
        var builder = UpdateChannelParticipant.Builder()
            .ChannelId(channelId)
            .Date(date)
            .ActorId(actorUserId)
            .UserId(joinedUserId)
            .NewParticipant(participant.AsSpan())
            .Qts(0);
        if (invite != null)
        {
            builder = builder.Invite(invite.InviteBytes);
        }
        using TLUpdate update = builder.Build();
        return update.AsSpan().ToArray();
    }

    private static byte[] BuildPendingUpdateBytes(InvitePeerSelection peer,
        IReadOnlyCollection<PendingInviteImporter> pending)
    {
        var recentRequesters = new VectorOfLong();
        foreach (long userId in pending.OrderByDescending(x => x.Date)
                     .ThenBy(x => x.UserId).Take(3).Select(x => x.UserId))
        {
            recentRequesters.Append(userId);
        }
        using TLPeer updatePeer = PeerResolver.BuildPeer(peer.IsChannel
            ? TLPeer.PeerType.PeerChannel
            : TLPeer.PeerType.PeerChat, peer.ChatId);
        using TLUpdate update = UpdatePendingJoinRequests.Builder()
            .Peer(updatePeer.AsSpan())
            .RequestsPending(pending.Count)
            .RecentRequesters(recentRequesters)
            .Build();
        return update.AsSpan().ToArray();
    }

    private static byte[] BuildNewMessageUpdateBytes(StoredMessageWrite write,
        bool channel)
    {
        using TLUpdate update = channel
            ? UpdateNewChannelMessage.Builder()
                .Message(write.Bytes)
                .Pts(write.Pts)
                .PtsCount(1)
                .Build()
            : UpdateNewMessage.Builder()
                .Message(write.Bytes)
                .Pts(write.Pts)
                .PtsCount(1)
                .Build();
        return update.AsSpan().ToArray();
    }

    private static TLUpdates ErrorUpdates(string message) =>
        (TLUpdates)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));
}
