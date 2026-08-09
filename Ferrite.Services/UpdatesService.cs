// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System;
using System.Collections.ObjectModel;
using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.updates;
using Ferrite.Utils;

namespace Ferrite.Services;

public class UpdatesService : IUpdatesService
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IUserRepository _userRepository;

    protected readonly ISessionService _sessions;
    protected readonly IMessagePipe _pipe;
    protected readonly IUnitOfWork _unitOfWork;
    protected readonly IUpdatesContextFactory _updatesContextFactory;
    protected readonly ILogger _log;

    public UpdatesService(ISessionService sessions, IMessagePipe pipe,
        IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChatRepository chatRepository, IUserRepository userRepository, IUpdatesContextFactory updatesContextFactory, ILogger log)
    {
        _authorizationRepository = authorizationRepository;
        _chatRepository = chatRepository;
        _userRepository = userRepository;

        _sessions = sessions;
        _pipe = pipe;
        _unitOfWork = unitOfWork;
        _updatesContextFactory = updatesContextFactory;
        _log = log;
    }

    public async Task<bool> EnqueueUpdate(long userId, TLUpdate update)
    {
        return await EnqueueUpdate(userId, update, UpdateDeliveryScope.All);
    }

    public async Task<bool> EnqueueUpdate(long userId, TLUpdate update,
        UpdateDeliveryScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        byte[] updateBytes;
        List<byte[]> relatedUsers;
        List<byte[]> relatedChats;
        try
        {
            (relatedUsers, relatedChats) = await GetRelatedObjects(update);
            updateBytes = update.AsSpan().ToArray();
        }
        finally
        {
            update.Dispose();
        }

        using var user = _userRepository.GetUser(userId);
        if (user == null) return false;

        var phone = Encoding.UTF8.GetString(user.Value.AsUser().Phone);
        // TODO: resolve active sessions directly by userId once available.
        var authorizations = await _authorizationRepository.GetAuthorizationsAsync(phone);
        var ownedAuthKeyIds = new HashSet<long>();
        var candidateAuthKeyIds = new List<long>();
        foreach (var authorization in authorizations)
        {
            var info = authorization.AsAuthInfo();
            if (info.UserId == userId && info.LoggedIn &&
                ownedAuthKeyIds.Add(info.AuthKeyId))
            {
                candidateAuthKeyIds.Add(info.AuthKeyId);
            }
        }

        // A targeted scope is an authorization boundary, not a best-effort filter.
        // Reject the whole request if any explicitly targeted key is not owned by the
        // user so a caller cannot accidentally route a device update across accounts.
        if (!scope.AreTargetsOwnedBy(ownedAuthKeyIds))
        {
            return false;
        }

        var delivered = false;
        foreach (long targetAuthKeyId in candidateAuthKeyIds)
        {
            if (!scope.Includes(targetAuthKeyId))
            {
                continue;
            }

            var updatesCtx = _updatesContextFactory.GetUpdatesContext(targetAuthKeyId, userId);
            int seq = await updatesCtx.IncrementSeq();
            using var updates = BuildUpdates(updateBytes, relatedUsers, relatedChats, seq);
            byte[] data = updates.AsSpan().ToArray();
            var sessions = await _sessions.GetSessionsAsync(targetAuthKeyId);
            _log.Debug($"📣 EnqueueUpdate user:{userId} target-auth:{targetAuthKeyId} " +
                       $"seq:{seq} sessions:{sessions.Count}");
            foreach (var s in sessions)
            {
                MTProtoMessage message = new MTProtoMessage
                {
                    Data = data,
                    SessionId = s.SessionId,
                    IsContentRelated = true,
                    IsResponse = false,
                    MessageType = MTProtoMessageType.Updates,
                };
                var bytes = MTProtoMessageEnvelope.Serialize(message);
                bool written = await _pipe.WriteMessageAsync(
                    MessagePipeChannels.ForNode(s.NodeId), bytes);
                delivered |= written;
            }
        }

        return delivered;
    }

    protected async Task<(List<byte[]> Users, List<byte[]> Chats)> GetRelatedObjects(TLUpdate update)
    {
        List<byte[]> users = new();
        List<byte[]> chats = new();
        var seenUsers = new HashSet<long>();
        var seenChats = new HashSet<long>();

        if (update.Constructor == Constructors.baseLayer_UpdateNewMessage ||
            update.Constructor == Constructors.baseLayer_UpdateNewChannelMessage)
        {
            using var message = update.Constructor == Constructors.baseLayer_UpdateNewMessage
                ? update.AsUpdateNewMessage().Get_Message()
                : update.AsUpdateNewChannelMessage().Get_Message();
            var (messageUserIds, messageChatIds) = ReadMessageRelationIds(message);
            foreach (long messageUserId in messageUserIds)
            {
                AddUser(users, seenUsers, messageUserId);
            }
            foreach (long messageChatId in messageChatIds)
            {
                await AddChat(chats, seenChats, messageChatId);
            }
        }
        else if (update.Constructor == Constructors.baseLayer_UpdateChatParticipants)
        {
            var (chatId, userIds) = ReadUpdateChatParticipants(update);
            if (chatId > 0)
            {
                await AddChat(chats, seenChats, chatId);
            }
            foreach (long participantUserId in userIds)
            {
                AddUser(users, seenUsers, participantUserId);
            }
        }
        else if (update.Constructor == Constructors.baseLayer_UpdateChat)
        {
            long chatId = update.AsUpdateChat().ChatId;
            if (chatId > 0)
            {
                await AddChat(chats, seenChats, chatId);
            }
        }
        else if (update.Constructor == Constructors.baseLayer_UpdateUser)
        {
            long userId = update.AsUpdateUser().UserId;
            if (userId > 0)
            {
                AddUser(users, seenUsers, userId);
            }
        }
        // Channel updates pushed to members must carry the channel row: a client that
        // has never seen the channel (e.g. it was just invited) cannot resolve a bare
        // channel_id and silently drops the update.
        else if (update.Constructor == Constructors.baseLayer_UpdateChannel)
        {
            long channelId = update.AsUpdateChannel().ChannelId;
            if (channelId > 0)
            {
                await AddChat(chats, seenChats, channelId);
            }
        }
        else if (update.Constructor == Constructors.baseLayer_UpdateChannelUserTyping)
        {
            var (channelId, fromUserId) = ReadUpdateChannelUserTyping(update);
            if (channelId > 0)
            {
                await AddChat(chats, seenChats, channelId);
            }
            if (fromUserId > 0)
            {
                AddUser(users, seenUsers, fromUserId);
            }
        }
        else if (update.Constructor == Constructors.baseLayer_UpdatePinnedChannelMessages)
        {
            long channelId = update.AsUpdatePinnedChannelMessages().ChannelId;
            if (channelId > 0)
            {
                await AddChat(chats, seenChats, channelId);
            }
        }
        else if (update.Constructor == Constructors.baseLayer_UpdateDeleteChannelMessages)
        {
            long channelId = update.AsUpdateDeleteChannelMessages().ChannelId;
            if (channelId > 0)
            {
                await AddChat(chats, seenChats, channelId);
            }
        }
        else if (update.Constructor == Constructors.baseLayer_UpdateChannelPinnedTopic)
        {
            long channelId = update.AsUpdateChannelPinnedTopic().ChannelId;
            if (channelId > 0)
            {
                await AddChat(chats, seenChats, channelId);
            }
        }
        else if (update.Constructor == Constructors.baseLayer_UpdateChannelPinnedTopics)
        {
            long channelId = update.AsUpdateChannelPinnedTopics().ChannelId;
            if (channelId > 0)
            {
                await AddChat(chats, seenChats, channelId);
            }
        }
        else if (update.Constructor == Constructors.baseLayer_UpdateChannelViewForumAsMessages)
        {
            long channelId = update.AsUpdateChannelViewForumAsMessages().ChannelId;
            if (channelId > 0)
            {
                await AddChat(chats, seenChats, channelId);
            }
        }
        else if (update.Constructor == Constructors.baseLayer_UpdateChatDefaultBannedRights)
        {
            long bannedRightsPeerId = 0;
            var rights = update.AsUpdateChatDefaultBannedRights();
            if (TryReadPeer(rights.Get_PeerView(), out var peer) &&
                peer.Type is TLPeer.PeerType.PeerChat or TLPeer.PeerType.PeerChannel)
            {
                bannedRightsPeerId = peer.Id;
            }
            if (bannedRightsPeerId > 0)
            {
                await AddChat(chats, seenChats, bannedRightsPeerId);
            }
        }
        // Reaction updates must carry the dialog chat/channel row plus the recent
        // reactor user rows so a receiving client can render the reaction senders.
        else if (update.Constructor == Constructors.baseLayer_UpdateMessageReactions)
        {
            var (reactionsChatId, reactionsUserIds) = ReadUpdateMessageReactions(update);
            if (reactionsChatId > 0)
            {
                await AddChat(chats, seenChats, reactionsChatId);
            }
            foreach (long reactorUserId in reactionsUserIds)
            {
                AddUser(users, seenUsers, reactorUserId);
            }
        }
        // Secret-chat lifecycle updates carry only user ids inside the encrypted-chat
        // row. Hydrate both peers so a device receiving its first updateEncryption can
        // resolve the conversation immediately.
        else if (update.Constructor == Constructors.baseLayer_UpdateEncryption)
        {
            var (adminId, participantId) = ReadEncryptedChatRelationIds(
                update.AsUpdateEncryption().Get_ChatView());
            if (adminId > 0)
            {
                AddUser(users, seenUsers, adminId);
            }
            if (participantId > 0)
            {
                AddUser(users, seenUsers, participantId);
            }
        }
        // 1:1 call updates carry only user ids inside the phone-call row. Hydrate
        // both participants so a device receiving phoneCallRequested can resolve its
        // peer immediately. Discarded/empty variants carry no ids, and
        // updatePhoneCallSignalingData needs no user hydration.
        else if (update.Constructor == Constructors.baseLayer_UpdatePhoneCall)
        {
            var (adminId, participantId) = ReadPhoneCallRelationIds(
                update.AsUpdatePhoneCall().Get_PhoneCallView());
            if (adminId > 0)
            {
                AddUser(users, seenUsers, adminId);
            }
            if (participantId > 0)
            {
                AddUser(users, seenUsers, participantId);
            }
        }

        // Group-call updates carry bare ids: updateGroupCall names the hosting chat
        // and updateGroupCallParticipants names each participant's join-as peer.
        // Hydrate both so a client that has never seen the chat, or a member who
        // joined as a channel, resolves immediately instead of dropping the update.
        // updateGroupCallConnection is deliberately absent: connection credentials
        // are built into the join RPC result and never enqueued or broadcast.
        else if (update.Constructor == Constructors.baseLayer_UpdateGroupCall)
        {
            long groupCallChatId = update.AsUpdateGroupCall().ChatId;
            if (groupCallChatId > 0)
            {
                await AddChat(chats, seenChats, groupCallChatId);
            }
        }
        else if (update.Constructor == Constructors.baseLayer_UpdateGroupCallParticipants)
        {
            var (participantUserIds, participantChatIds) =
                ReadGroupCallParticipantRelationIds(update);
            foreach (long participantUserId in participantUserIds)
            {
                AddUser(users, seenUsers, participantUserId);
            }
            foreach (long participantChatId in participantChatIds)
            {
                await AddChat(chats, seenChats, participantChatId);
            }
        }

        return (users, chats);
    }

    // The join-as peers of one updateGroupCallParticipants payload. The vector is
    // read synchronously in full before any await: Vector and the participant view
    // are ref structs and cannot cross one.
    protected static (List<long> UserIds, List<long> ChatIds)
        ReadGroupCallParticipantRelationIds(TLUpdate update)
    {
        var userIds = new List<long>();
        var chatIds = new List<long>();
        var participants = update.AsUpdateGroupCallParticipants().Participants;
        int count = participants.Count;
        for (int i = 0; i < count; i++)
        {
            var participant = (GroupCallParticipant)participants.ReadTLObject();
            if (participant.Constructor != Constructors.baseLayer_GroupCallParticipant ||
                !TryReadPeer(participant.Get_PeerView(), out var peer) || peer.Id <= 0)
            {
                continue;
            }
            if (peer.Type == TLPeer.PeerType.PeerUser)
            {
                userIds.Add(peer.Id);
            }
            else
            {
                chatIds.Add(peer.Id);
            }
        }

        return (userIds, chatIds);
    }

    protected static (long AdminId, long ParticipantId) ReadPhoneCallRelationIds(
        PhoneCallView call)
    {
        if (call.Is(out PhoneCallWaiting waiting))
        {
            return (waiting.AdminId, waiting.ParticipantId);
        }
        if (call.Is(out PhoneCallRequested requested))
        {
            return (requested.AdminId, requested.ParticipantId);
        }
        if (call.Is(out PhoneCallAccepted accepted))
        {
            return (accepted.AdminId, accepted.ParticipantId);
        }
        if (call.Is(out PhoneCall active))
        {
            return (active.AdminId, active.ParticipantId);
        }

        return default;
    }

    protected static (long AdminId, long ParticipantId) ReadEncryptedChatRelationIds(
        EncryptedChatView chat)
    {
        if (chat.Is(out EncryptedChatWaiting waiting))
        {
            return (waiting.AdminId, waiting.ParticipantId);
        }
        if (chat.Is(out EncryptedChatRequested requested))
        {
            return (requested.AdminId, requested.ParticipantId);
        }
        if (chat.Is(out EncryptedChat active))
        {
            return (active.AdminId, active.ParticipantId);
        }

        return default;
    }

    protected static (long ChatId, List<long> UserIds) ReadUpdateMessageReactions(
        TLUpdate update)
    {
        var reactionsUpdate = update.AsUpdateMessageReactions();
        long chatId = 0;
        var userIds = new List<long>();
        if (TryReadPeer(reactionsUpdate.Get_PeerView(), out var peer))
        {
            if (peer.Type is TLPeer.PeerType.PeerChat or TLPeer.PeerType.PeerChannel)
            {
                chatId = peer.Id;
            }
            else
            {
                userIds.Add(peer.Id);
            }
        }

        var reactions = (MessageReactions)reactionsUpdate.Reactions;
        if (reactions.Constructor == Constructors.baseLayer_MessageReactions &&
            reactions.Flags[1])
        {
            var recent = reactions.RecentReactions;
            int count = recent.Count;
            for (int i = 0; i < count; i++)
            {
                var peerReaction = (MessagePeerReaction)recent.ReadTLObject();
                if (peerReaction.Constructor == Constructors.baseLayer_MessagePeerReaction &&
                    TryReadPeer(peerReaction.Get_PeerIdView(), out var reactorPeer) &&
                    reactorPeer.Type == TLPeer.PeerType.PeerUser)
                {
                    userIds.Add(reactorPeer.Id);
                }
            }
        }

        return (chatId, userIds);
    }

    protected static (long ChannelId, long FromUserId) ReadUpdateChannelUserTyping(TLUpdate update)
    {
        var typing = update.AsUpdateChannelUserTyping();
        long fromUserId = 0;
        if (TryReadPeer(typing.Get_FromIdView(), out var fromPeer) &&
            fromPeer.Type == TLPeer.PeerType.PeerUser)
        {
            fromUserId = fromPeer.Id;
        }

        return (typing.ChannelId, fromUserId);
    }

    protected static (long ChatId, List<long> UserIds) ReadUpdateChatParticipants(TLUpdate update)
    {
        var participants = update.AsUpdateChatParticipants().Get_ParticipantsView();
        return ReadChatParticipants(participants);
    }

    internal static (List<long> UserIds, List<long> ChatIds) ReadMessageRelationIds(TLMessage message)
    {
        var userIds = new List<long>();
        var chatIds = new List<long>();
        if (message.Type == TLMessage.MessageType.Message)
        {
            var messageBody = message.AsMessage();
            if (messageBody.Flags[8] && TryReadPeer(
                    messageBody.Get_FromIdView(), out var fromPeer))
            {
                AddPeerIds(fromPeer, userIds, chatIds);
            }
            if (TryReadPeer(messageBody.Get_PeerIdView(), out var peer))
            {
                AddPeerIds(peer, userIds, chatIds);
            }
            return (userIds, chatIds);
        }

        if (message.Type != TLMessage.MessageType.MessageService)
        {
            return (userIds, chatIds);
        }

        var service = message.AsMessageService();
        if (service.Flags[8] && TryReadPeer(service.Get_FromIdView(),
                out var serviceFrom))
        {
            AddPeerIds(serviceFrom, userIds, chatIds);
        }
        if (TryReadPeer(service.Get_PeerIdView(), out var servicePeer))
        {
            AddPeerIds(servicePeer, userIds, chatIds);
        }

        MessageActionView action = service.Get_ActionView();
        var actionUserIds = ReadActionUserIds(action);
        foreach (long actionUserId in actionUserIds)
        {
            userIds.Add(actionUserId);
        }
        foreach (long actionChatId in ReadActionChatIds(action))
        {
            chatIds.Add(actionChatId);
        }

        return (userIds, chatIds);
    }

    // Wraps a single slim update in an `updates` container so it can be encrypted
    // and pushed as MTProtoMessageType.Updates.
    protected static TLUpdates BuildUpdates(ReadOnlySpan<byte> update,
        IReadOnlyCollection<byte[]> relatedUsers, IReadOnlyCollection<byte[]> relatedChats, int seq)
    {
        var updateVector = new Vector();
        updateVector.AppendTLObject(update);
        var userVector = new Vector();
        foreach (var user in relatedUsers)
        {
            userVector.AppendTLObject(user);
        }
        var chatVector = new Vector();
        foreach (var chat in relatedChats)
        {
            chatVector.AppendTLObject(chat);
        }

        return Updates.Builder()
            .UpdatesProperty(updateVector)
            .Users(userVector)
            .Chats(chatVector)
            .Seq(seq)
            .Date((int)DateTimeOffset.Now.ToUnixTimeSeconds())
            .Build();
    }

    protected static void AddPeerIds((TLPeer.PeerType Type, long Id) peer, List<long> users,
        List<long> chats)
    {
        if (peer.Type == TLPeer.PeerType.PeerUser)
        {
            users.Add(peer.Id);
        }
        else if (peer.Type is TLPeer.PeerType.PeerChat or TLPeer.PeerType.PeerChannel)
        {
            chats.Add(peer.Id);
        }
    }

    protected void AddUser(List<byte[]> users, HashSet<long> seenUsers, long userId)
    {
        if (!seenUsers.Add(userId))
        {
            return;
        }

        using var user = _userRepository.GetUser(userId);
        if (user != null)
        {
            users.Add(user.Value.AsSpan().ToArray());
        }
    }

    protected async Task AddChat(List<byte[]> chats, HashSet<long> seenChats, long chatId)
    {
        if (!seenChats.Add(chatId))
        {
            return;
        }

        using var chat = await _chatRepository.GetChatAsync(chatId);
        if (chat != null)
        {
            chats.Add(chat.Value.AsSpan().ToArray());
        }
    }

    protected static (long ChatId, List<long> UserIds) ReadChatParticipants(ChatParticipantsView participants)
    {
        var userIds = new List<long>();
        if (!participants.Is(out ChatParticipants chatParticipants))
        {
            if (participants.Is(out ChatParticipantsForbidden forbidden))
            {
                return (forbidden.ChatId, userIds);
            }
            return (0, userIds);
        }

        var participantVector = chatParticipants.Participants;
        int count = participantVector.Count;
        for (int i = 0; i < count; i++)
        {
            ChatParticipantView participant = participantVector.ReadTLObject();
            if (participant.Is(out ChatParticipant member))
            {
                userIds.Add(member.UserId);
            }
            else if (participant.Is(out ChatParticipantCreator creator))
            {
                userIds.Add(creator.UserId);
            }
            else if (participant.Is(out ChatParticipantAdmin admin))
            {
                userIds.Add(admin.UserId);
            }
        }

        return (chatParticipants.ChatId, userIds);
    }

    protected static List<long> ReadActionUserIds(MessageActionView action)
    {
        var userIds = new List<long>();
        if (action.Is(out MessageActionChatCreate create))
        {
            var users = create.Users;
            for (int i = 0; i < users.Count; i++)
            {
                userIds.Add(users[i]);
            }
        }
        else if (action.Is(out MessageActionChatAddUser addUser))
        {
            var users = addUser.Users;
            for (int i = 0; i < users.Count; i++)
            {
                userIds.Add(users[i]);
            }
        }
        else if (action.Is(out MessageActionChatDeleteUser deleteUser))
        {
            userIds.Add(deleteUser.UserId);
        }
        else if (action.Is(out MessageActionChatJoinedByLink joinedByLink))
        {
            userIds.Add(joinedByLink.InviterId);
        }
        else if (action.Is(out MessageActionInviteToGroupCall groupCallInvite))
        {
            VectorOfLong users = groupCallInvite.Users;
            for (int i = 0; i < users.Count; i++)
            {
                userIds.Add(users[i]);
            }
        }

        return userIds;
    }

    protected static List<long> ReadActionChatIds(MessageActionView action)
    {
        var chatIds = new List<long>();
        if (action.Is(out MessageActionChatMigrateTo migrateTo))
        {
            chatIds.Add(migrateTo.ChannelId);
        }
        else if (action.Is(out MessageActionChannelMigrateFrom migrateFrom))
        {
            chatIds.Add(migrateFrom.ChatId);
        }

        return chatIds;
    }

    protected static bool TryReadPeer(PeerView peer, out (TLPeer.PeerType Type, long Id) value)
    {
        if (peer.Is(out PeerUser user))
        {
            value = (TLPeer.PeerType.PeerUser, user.UserId);
            return true;
        }
        if (peer.Is(out PeerChat chat))
        {
            value = (TLPeer.PeerType.PeerChat, chat.ChatId);
            return true;
        }
        if (peer.Is(out PeerChannel channel))
        {
            value = (TLPeer.PeerType.PeerChannel, channel.ChannelId);
            return true;
        }

        value = default;
        return false;
    }

    public async Task<int> IncrementUpdatesSequence(long authKeyId)
    {
        /*var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
        var updatesCtx = _updatesContextFactory.GetUpdatesContext(authKeyId, auth.UserId);
        return await updatesCtx.IncrementSeq();*/
        throw new NotImplementedException();
    }
}
