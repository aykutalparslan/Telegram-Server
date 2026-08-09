// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services;

/// <summary>
/// Builds caller-facing update containers and delivers fresh update values to
/// live recipients. Persistence, message ids, rights, search, and dialog/history
/// assembly stay with their owning services and pipelines.
/// </summary>
public sealed class UpdateFanout
{
    private readonly IChannelMessagesRepository _channelMessagesRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IUserRepository _userRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IUpdatesService _updates;
    private readonly IUpdatesContextFactory _updatesContextFactory;

    public UpdateFanout(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IUserRepository userRepository, IUpdatesService updates,
        IUpdatesContextFactory updatesContextFactory)
    {
        _channelMessagesRepository = channelMessagesRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;
        _userRepository = userRepository;

        _unitOfWork = unitOfWork;
        _updates = updates;
        _updatesContextFactory = updatesContextFactory;
    }

    public async Task EnqueueNewMessageAsync(long recipientId, byte[] messageBytes,
        int pts)
    {
        TLUpdate update = UpdateNewMessage.Builder()
            .Message(messageBytes)
            .Pts(pts)
            .PtsCount(1)
            .Build();
        await _updates.EnqueueUpdate(recipientId, update);
    }

    public async Task EnqueueNewChannelMessageAsync(long recipientId,
        byte[] messageBytes, int pts)
    {
        TLUpdate update = UpdateNewChannelMessage.Builder()
            .Message(messageBytes)
            .Pts(pts)
            .PtsCount(1)
            .Build();
        await _updates.EnqueueUpdate(recipientId, update);
    }

    public async Task EnqueueMessageReactionsAsync(long recipientId, byte[] peerBytes,
        int msgId, byte[] reactionsBytes)
    {
        TLUpdate update = BuildMessageReactionsUpdate(peerBytes, msgId, reactionsBytes);
        await _updates.EnqueueUpdate(recipientId, update);
    }

    public async Task EnqueueUpdateChannelAsync(long recipientId, long channelId)
    {
        TLUpdate update = UpdateChannel.Builder().ChannelId(channelId).Build();
        await _updates.EnqueueUpdate(recipientId, update);
    }

    public async Task EnqueueRecentReactionsAsync(long userId)
    {
        TLUpdate update = UpdateRecentReactions.Builder().Build();
        await _updates.EnqueueUpdate(userId, update);
    }

    public async Task EnqueueSerializedAsync(long recipientId, byte[] updateBytes)
    {
        await _updates.EnqueueUpdate(recipientId,
            new TLUpdate(updateBytes, 0, updateBytes.Length));
    }

    public async Task EnqueueSerializedAsync(IEnumerable<long> recipientIds,
        IReadOnlyCollection<byte[]> updateBytes)
    {
        foreach (long recipientId in recipientIds)
        {
            foreach (byte[] bytes in updateBytes)
            {
                await EnqueueSerializedAsync(recipientId, bytes);
            }
        }
    }

    public async Task<TLUpdates> BuildReactionsResultAsync(long userId,
        byte[] peerBytes, int msgId, byte[] reactionsBytes,
        IReadOnlyCollection<ReactionEntry> entries, long reactionConfigChatId,
        bool incrementSeq, long? authKeyId = null)
    {
        int seq = 0;
        if (incrementSeq)
        {
            IUpdatesContext seqCtx = _updatesContextFactory
                .GetUpdatesContext(authKeyId, userId);
            seq = await seqCtx.IncrementSeq();
        }

        List<byte[]> chatBytes = reactionConfigChatId > 0
            ? await GetChatBytesForViewerAsync(userId, new[] { reactionConfigChatId })
            : new List<byte[]>();
        var reactorIds = new HashSet<long> { userId };
        foreach (ReactionEntry entry in entries)
        {
            reactorIds.Add(entry.UserId);
        }

        using TLUpdate update = BuildMessageReactionsUpdate(peerBytes, msgId,
            reactionsBytes);
        return BuildUpdates(new[] { update.AsSpan().ToArray() }, reactorIds,
            chatBytes, (int)DateTimeOffset.Now.ToUnixTimeSeconds(), seq);
    }

    public async Task<TLUpdates> BuildChannelSentResultAsync(long authKeyId,
        ChannelSentBatch sent)
    {
        IUpdatesContext seqCtx = _updatesContextFactory
            .GetUpdatesContext(authKeyId, sent.UserId);
        int seq = await seqCtx.IncrementSeq();
        var updateBytes = new List<byte[]>(2);
        using (TLUpdate updateMessageId = UpdateMessageID.Builder()
                   .Id(sent.Id)
                   .RandomId(sent.RandomId)
                   .Build())
        {
            updateBytes.Add(updateMessageId.AsSpan().ToArray());
        }
        using (TLUpdate updateNewChannelMessage = UpdateNewChannelMessage.Builder()
                   .Message(sent.MessageBytes)
                   .Pts(sent.Pts)
                   .PtsCount(1)
                   .Build())
        {
            updateBytes.Add(updateNewChannelMessage.AsSpan().ToArray());
        }

        var userIds = new HashSet<long> { sent.UserId };
        var chatIds = new HashSet<long> { sent.ChannelId };
        using (var message = new TLMessage(sent.MessageBytes, 0,
                   sent.MessageBytes.Length))
        {
            MessageStore.AddMessageRelatedPeers(message, userIds, chatIds);
        }
        List<byte[]> chats = await GetChatBytesForViewerAsync(sent.UserId, chatIds);
        return BuildUpdates(updateBytes, userIds, chats, sent.Date, seq);
    }

    public async Task<TLUpdates> BuildMediaAlbumSentResultAsync(long authKeyId,
        long actorUserId, IReadOnlyList<MediaSentBatch> sentItems,
        IReadOnlyCollection<long> relatedUserIds)
    {
        int seq = await _updatesContextFactory
            .GetUpdatesContext(authKeyId, actorUserId).IncrementSeq();
        var updateBytes = new List<byte[]>(sentItems.Count * 2);
        var userIds = new HashSet<long>(relatedUserIds) { actorUserId };
        var chatIds = new HashSet<long>();
        foreach (MediaSentBatch sent in sentItems)
        {
            using (TLUpdate updateMessageId = UpdateMessageID.Builder()
                       .Id(sent.Id)
                       .RandomId(sent.RandomId)
                       .Build())
            {
                updateBytes.Add(updateMessageId.AsSpan().ToArray());
            }

            using TLUpdate updateNewMessage = sent.PeerType == TLPeer.PeerType.PeerChannel
                ? UpdateNewChannelMessage.Builder()
                    .Message(sent.MessageBytes)
                    .Pts(sent.Pts)
                    .PtsCount(1)
                    .Build()
                : UpdateNewMessage.Builder()
                    .Message(sent.MessageBytes)
                    .Pts(sent.Pts)
                    .PtsCount(1)
                    .Build();
            updateBytes.Add(updateNewMessage.AsSpan().ToArray());

            using var message = new TLMessage(sent.MessageBytes, 0,
                sent.MessageBytes.Length);
            MessageStore.AddMessageRelatedPeers(message, userIds, chatIds);
            if (sent.PeerType == TLPeer.PeerType.PeerUser)
            {
                userIds.Add(sent.PeerId);
            }
            else
            {
                chatIds.Add(sent.PeerId);
            }
        }

        List<byte[]> chats = await GetChatBytesForViewerAsync(actorUserId, chatIds);
        int date = sentItems.Count == 0
            ? (int)DateTimeOffset.Now.ToUnixTimeSeconds()
            : sentItems[^1].Date;
        return BuildUpdates(updateBytes, userIds, chats, date, seq);
    }

    public async Task<TLUpdates> BuildPinnedMessagesResultAsync(long userId,
        TLPeer.PeerType peerType, long peerId, IReadOnlyList<int> messageIds,
        bool pinned, int pts, int ptsCount)
    {
        List<byte[]> chatBytes = peerType == TLPeer.PeerType.PeerChat
            ? await GetChatBytesAsync(new[] { peerId })
            : new List<byte[]>();
        var userIds = new List<long>();
        if (peerType == TLPeer.PeerType.PeerUser)
        {
            userIds.Add(peerId);
            if (peerId != userId)
            {
                userIds.Add(userId);
            }
        }

        using TLUpdate update = BuildPinnedMessagesUpdate(peerType, peerId,
            messageIds, pinned, pts, ptsCount);
        return BuildUpdates(new[] { update.AsSpan().ToArray() }, userIds, chatBytes,
            (int)DateTimeOffset.Now.ToUnixTimeSeconds(), seq: 0);
    }

    public async Task<TLUpdates> CompleteBasicGroupServiceResultAsync(
        IReadOnlyCollection<long> participantIds,
        IReadOnlyCollection<(long ParticipantId, byte[] UpdateBytes)> liveUpdates,
        IReadOnlyCollection<byte[]> callerUpdateBytes, byte[] chatBytes,
        byte[]? sharedUpdateBytes, int date)
    {
        foreach (var (participantId, updateBytes) in liveUpdates)
        {
            await EnqueueSerializedAsync(participantId, updateBytes);
        }

        var resultBytes = new List<byte[]>(callerUpdateBytes);
        if (sharedUpdateBytes != null)
        {
            resultBytes.Add(sharedUpdateBytes);
            foreach (long participantId in participantIds)
            {
                await EnqueueSerializedAsync(participantId, sharedUpdateBytes);
            }
        }

        // Seq 0 is deliberate: live enqueues above own the per-session seq numbers.
        return BuildUpdates(resultBytes, participantIds, new[] { chatBytes }, date,
            seq: 0);
    }

    public Task<List<long>> GetOtherActiveChannelMemberIdsAsync(long channelId,
        long actorUserId) => GetActiveMemberIdsAsync(channelId, actorUserId);

    // Basic groups and channels both persist their membership as chat-participant
    // rows keyed by the chat id, so one walk serves both.
    public async Task<List<long>> GetActiveMemberIdsAsync(long chatId,
        long? excludeUserId)
    {
        IReadOnlyCollection<TLChatParticipantInfo> participants =
            await _chatParticipantsRepository.GetParticipantsAsync(chatId);
        var memberIds = new List<long>();
        foreach (TLChatParticipantInfo participant in participants)
        {
            using var row = participant;
            int role = row.AsChatParticipantInfo().Role;
            if (role is (int)ChatParticipantRole.Banned or
                (int)ChatParticipantRole.Left)
            {
                continue;
            }
            long userId = row.AsChatParticipantInfo().UserId;
            if (userId != excludeUserId)
            {
                memberIds.Add(userId);
            }
        }
        return memberIds;
    }

    // Group-call updates are viewer-correct, so each member gets a row built for
    // it rather than one shared payload. The builder returns null for a member
    // that should not receive this update at all; the enqueue consumes and
    // disposes every update it is handed.
    public async Task<int> PushGroupCallUpdatesAsync(long peerChatId, long? excludeUserId,
        Func<long, Task<TLUpdate?>> buildForMember) =>
        await PushGroupCallUpdatesToAsync(
            await GetActiveMemberIdsAsync(peerChatId, excludeUserId), buildForMember);

    // The same delivery for a recipient list the caller already knows. An E2E
    // conference has no hosting chat to walk, so its membership is the call's own
    // participant list and only the caller can produce it.
    public async Task<int> PushGroupCallUpdatesToAsync(IReadOnlyList<long> memberIds,
        Func<long, Task<TLUpdate?>> buildForMember)
    {
        int delivered = 0;
        foreach (long memberId in memberIds)
        {
            TLUpdate? update = await buildForMember(memberId);
            if (update == null)
            {
                continue;
            }
            if (await _updates.EnqueueUpdate(memberId, update.Value))
            {
                delivered++;
            }
        }

        return delivered;
    }

    public async Task PushUpdateChannelToOtherMembersAsync(long channelId,
        long actorUserId)
    {
        List<long> memberIds = await GetOtherActiveChannelMemberIdsAsync(channelId,
            actorUserId);
        foreach (long memberId in memberIds)
        {
            await EnqueueUpdateChannelAsync(memberId, channelId);
        }
    }

    public async Task PushDeleteChannelMessagesAsync(long channelId, long actorUserId,
        IReadOnlyList<int> deletedIds, int pts, int ptsCount)
    {
        PersistDeleteChannelMessages(channelId, deletedIds, pts, ptsCount);
        await DeliverDeleteChannelMessagesAsync(channelId, actorUserId, deletedIds,
            pts, ptsCount);
    }

    public void PersistDeleteChannelMessages(long channelId,
        IReadOnlyList<int> deletedIds, int pts, int ptsCount)
    {
        using TLUpdate durable = BuildDeleteChannelMessagesUpdate(channelId,
            deletedIds, pts, ptsCount);
        _channelMessagesRepository.PutUpdate(channelId, pts, durable);
    }

    public async Task DeliverDeleteChannelMessagesAsync(long channelId,
        long actorUserId, IReadOnlyList<int> deletedIds, int pts, int ptsCount)
    {
        List<long> memberIds = await GetOtherActiveChannelMemberIdsAsync(channelId,
            actorUserId);
        foreach (long memberId in memberIds)
        {
            TLUpdate update = BuildDeleteChannelMessagesUpdate(channelId, deletedIds,
                pts, ptsCount);
            await _updates.EnqueueUpdate(memberId, update);
        }
    }

    public async Task PushChannelServiceMessageAsync(long channelId, long actorUserId,
        byte[] messageBytes, int pts)
    {
        List<long> memberIds = await GetOtherActiveChannelMemberIdsAsync(channelId,
            actorUserId);
        foreach (long memberId in memberIds)
        {
            await EnqueueNewChannelMessageAsync(memberId, messageBytes, pts);
        }
    }

    public async Task PushSerializedToOtherChannelMembersAsync(long channelId,
        long actorUserId, IReadOnlyCollection<byte[]> updateBytes)
    {
        List<long> memberIds = await GetOtherActiveChannelMemberIdsAsync(channelId,
            actorUserId);
        await EnqueueSerializedAsync(memberIds, updateBytes);
    }

    public async Task PushUpdateChatAsync(long chatId, IEnumerable<long> recipientIds)
    {
        foreach (long recipientId in recipientIds)
        {
            TLUpdate update = UpdateChat.Builder().ChatId(chatId).Build();
            await _updates.EnqueueUpdate(recipientId, update);
        }
    }

    public async Task<TLUpdates> BuildChannelStateResultAsync(long authKeyId,
        long actorUserId, byte[] channelBytes, IReadOnlyCollection<long> extraUserIds,
        int date)
    {
        int seq = await _updatesContextFactory
            .GetUpdatesContext(authKeyId, actorUserId).IncrementSeq();
        long channelId;
        using (var channel = new TLChat(channelBytes, 0, channelBytes.Length))
        {
            channelId = channel.AsChannel().Id;
        }
        using TLUpdate update = UpdateChannel.Builder().ChannelId(channelId).Build();
        var userIds = new List<long> { actorUserId };
        userIds.AddRange(extraUserIds);
        return BuildUpdates(new[] { update.AsSpan().ToArray() }, userIds,
            new[] { channelBytes }, date, seq);
    }

    public async Task<TLUpdates> BuildForumResultAsync(long authKeyId,
        long actorUserId, byte[] channelBytes,
        IReadOnlyCollection<byte[]> updateBytes, int date,
        IReadOnlyCollection<long>? extraChatIds = null)
    {
        int seq = await _updatesContextFactory
            .GetUpdatesContext(authKeyId, actorUserId).IncrementSeq();
        var chats = new List<byte[]> { channelBytes };
        if (extraChatIds is { Count: > 0 })
        {
            long destinationId;
            using (var channel = new TLChat(channelBytes, 0, channelBytes.Length))
            {
                destinationId = channel.AsChannel().Id;
            }
            chats.AddRange(await GetChatBytesForViewerAsync(actorUserId,
                extraChatIds.Where(id => id != destinationId).Distinct()));
        }
        return BuildUpdates(updateBytes, new[] { actorUserId },
            chats, date, seq);
    }

    public async Task<int> AdvanceAndEnqueueDeleteMessagesAsync(long ownerId,
        IReadOnlyList<int> deletedIds, IUpdatesContext ownerContext)
    {
        if (deletedIds.Count == 0)
        {
            return await ownerContext.Pts();
        }

        int pts = await ownerContext.IncrementPts(deletedIds.Count);
        var ids = new VectorOfInt();
        foreach (int id in deletedIds)
        {
            ids.Append(id);
        }
        TLUpdate update = UpdateDeleteMessages.Builder()
            .Messages(ids)
            .Pts(pts)
            .PtsCount(deletedIds.Count)
            .Build();
        await _updates.EnqueueUpdate(ownerId, update);
        return pts;
    }

    public static TLUpdate BuildMessageReactionsUpdate(byte[] peerBytes, int msgId,
        byte[] reactionsBytes) => UpdateMessageReactions.Builder()
        .Peer(peerBytes)
        .MsgId(msgId)
        .Reactions(reactionsBytes)
        .Build();

    public static TLUpdate BuildPinnedMessagesUpdate(TLPeer.PeerType peerType,
        long peerId, IReadOnlyList<int> messageIds, bool pinned, int pts, int ptsCount)
    {
        var ids = new VectorOfInt();
        foreach (int messageId in messageIds)
        {
            ids.Append(messageId);
        }
        using TLPeer peer = PeerResolver.BuildPeer(peerType, peerId);
        return UpdatePinnedMessages.Builder()
            .Pinned(pinned)
            .Peer(peer.AsSpan())
            .Messages(ids)
            .Pts(pts)
            .PtsCount(ptsCount)
            .Build();
    }

    public static TLUpdate BuildDeleteChannelMessagesUpdate(long channelId,
        IReadOnlyList<int> deletedIds, int pts, int ptsCount)
    {
        var ids = new VectorOfInt();
        foreach (int id in deletedIds)
        {
            ids.Append(id);
        }
        return UpdateDeleteChannelMessages.Builder()
            .ChannelId(channelId)
            .Messages(ids)
            .Pts(pts)
            .PtsCount(ptsCount)
            .Build();
    }

    public TLUpdates BuildUpdates(IReadOnlyCollection<byte[]> updateBytes,
        IEnumerable<long> userIds, IReadOnlyCollection<byte[]> chatBytes,
        int date, int seq)
    {
        var updates = new Vector();
        foreach (byte[] bytes in updateBytes)
        {
            updates.AppendTLObject(bytes);
        }
        var users = new Vector();
        AppendUsers(ref users, userIds);
        var chats = new Vector();
        foreach (byte[] bytes in chatBytes)
        {
            chats.AppendTLObject(bytes);
        }
        return Updates.Builder()
            .UpdatesProperty(updates)
            .Users(users)
            .Chats(chats)
            .Date(date)
            .Seq(seq)
            .Build();
    }

    public void AppendUsers(ref Vector users, IEnumerable<long> userIds)
    {
        var seen = new HashSet<long>();
        foreach (long userId in userIds)
        {
            if (!seen.Add(userId))
            {
                continue;
            }
            using TLUser? user = _userRepository.GetUser(userId);
            if (user != null)
            {
                users.AppendTLObject(user.Value.AsSpan());
            }
        }
    }

    private async Task<List<byte[]>> GetChatBytesAsync(IEnumerable<long> chatIds)
    {
        var result = new List<byte[]>();
        foreach (long chatId in chatIds)
        {
            using TLChat? chat = await _chatRepository.GetChatAsync(chatId);
            if (chat != null)
            {
                result.Add(chat.Value.AsSpan().ToArray());
            }
        }
        return result;
    }

    public async Task<List<byte[]>> GetChatBytesForViewerAsync(long viewerUserId,
        IEnumerable<long> chatIds)
    {
        var result = new List<byte[]>();
        foreach (long chatId in chatIds)
        {
            byte[] rowBytes;
            bool isChannel;
            using (TLChat? chat = await _chatRepository.GetChatAsync(chatId))
            {
                if (chat == null)
                {
                    continue;
                }
                rowBytes = chat.Value.AsSpan().ToArray();
                isChannel = chat.Value.Type == TLChat.ChatType.Channel;
            }
            if (isChannel)
            {
                rowBytes = await ChannelRows.ForViewerAsync(
                    _chatParticipantsRepository, viewerUserId, chatId,
                    rowBytes);
            }
            result.Add(rowBytes);
        }
        return result;
    }
}
