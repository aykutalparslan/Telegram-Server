// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;
using Ferrite.Utils;

namespace Ferrite.Services.Dialogs;

public sealed record HistoryQuery(int OffsetId, int OffsetDate, int AddOffset,
    int Limit, int MaxId, int MinId);

public sealed record DialogQuery(int OffsetDate, int OffsetId, int Limit,
    DialogPeerKey? OffsetPeer, int FolderId = 0);

public sealed class DialogBuilder
{
    private readonly IChannelMessagesRepository _channelMessagesRepository;
    private readonly IMessageReactionsRepository _messageReactionsRepository;

    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IDialogOrganizationRepository _dialogOrganizationRepository;
    private readonly IDraftsRepository _draftsRepository;
    private readonly IMessageRepository _messageRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IUpdatesContextFactory _updatesContextFactory;
    private readonly ICounterFactory _counterFactory;
    private readonly IUpdatesStateService _updatesState;
    private readonly UpdateFanout _fanout;
    private readonly MessageExpiryStore _expiry;
    private readonly MentionScope _mentions;
    private readonly ILogger _log;

    public DialogBuilder(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IMessageReactionsRepository messageReactionsRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IDialogOrganizationRepository dialogOrganizationRepository, IDraftsRepository draftsRepository, IMessageRepository messageRepository,
        IUpdatesContextFactory updatesContextFactory, ICounterFactory counterFactory,
        IUpdatesStateService updatesState, UpdateFanout fanout,
        MessageExpiryStore expiry, MentionScope mentions, ILogger log)
    {
        _channelMessagesRepository = channelMessagesRepository;
        _messageReactionsRepository = messageReactionsRepository;

        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;
        _dialogOrganizationRepository = dialogOrganizationRepository;
        _draftsRepository = draftsRepository;
        _messageRepository = messageRepository;

        _unitOfWork = unitOfWork;
        _updatesContextFactory = updatesContextFactory;
        _counterFactory = counterFactory;
        _updatesState = updatesState;
        _fanout = fanout;
        _expiry = expiry;
        _mentions = mentions;
        _log = log;
    }

    public async Task<TLMessages> GetChannelHistoryAsync(long userId, long channelId,
        HistoryQuery query)
    {
        using TLChat? channel = await _chatRepository.GetChatAsync(channelId);
        if (channel == null || channel.Value.Type != TLChat.ChatType.Channel)
        {
            return (TLMessages)RpcErrorGenerator.GenerateError(400, "CHANNEL_INVALID"u8);
        }

        TLChatParticipantInfo? participant = await _chatParticipantsRepository
            .GetParticipantAsync(channelId, userId);
        bool member = participant != null && IsActiveParticipant(participant.Value);
        participant?.Dispose();
        if (!member)
        {
            return (TLMessages)RpcErrorGenerator.GenerateError(400, "CHANNEL_PRIVATE"u8);
        }

        List<MessageSnapshot> conversation = await ReadChannelConversationAsync(
            channelId);
        return await BuildChannelMessagesAsync(userId, channelId, conversation,
            query, conversation.Count, "GetChannelHistory");
    }

    public async Task<TLMessages> GetHistoryForPeerAsync(long userId,
        TLPeer.PeerType peerType, long peerId, HistoryQuery query, string caller)
    {
        List<MessageSnapshot> conversation = await ReadCommonConversationAsync(userId,
            peerType, peerId);
        return await BuildCommonMessagesAsync(userId, peerType, peerId, conversation,
            query, caller);
    }

    public async Task<List<MessageSnapshot>> ReadCommonConversationAsync(long userId,
        TLPeer.PeerType peerType, long peerId)
    {
        IReadOnlyCollection<TLSavedMessage> saved = await _messageRepository
            .GetMessagesAsync(userId);
        return ReadConversation(saved, new DialogPeerKey(peerType, peerId));
    }

    public async Task<List<MessageSnapshot>> ReadChannelConversationAsync(
        long channelId)
    {
        IReadOnlyCollection<TLSavedMessage> saved = await _channelMessagesRepository.GetMessagesAsync(channelId);
        return ReadConversation(saved, null);
    }

    public async Task<List<BoxMessage>> ReadCommonBoxAsync(long userId)
    {
        IReadOnlyCollection<TLSavedMessage> saved = await _messageRepository.GetMessagesAsync(userId);
        var result = new List<BoxMessage>();
        foreach (TLSavedMessage value in saved)
        {
            using TLSavedMessage savedMessage = value;
            TLMessage message = savedMessage.AsSavedMessage().Get_OriginalMessage();
            if (!MessageStore.TryReadStoredMessageInfo(message,
                    out StoredMessageInfo info))
            {
                continue;
            }
            result.Add(new BoxMessage(
                new MessageSnapshot(info.Id, info.Date, info.Bytes), info.PeerType,
                info.PeerId));
        }
        return result;
    }

    public async Task<TLMessages> BuildCommonMessagesAsync(long userId,
        TLPeer.PeerType peerType, long peerId,
        IReadOnlyList<MessageSnapshot> conversation, HistoryQuery query,
        string caller)
    {
        List<byte[]> selected = SelectHistory(conversation, query,
            allowUnlimited: false, out int startIndex);

        var relatedUserIds = new HashSet<long>();
        var relatedChatIds = new HashSet<long>();
        if (peerType == TLPeer.PeerType.PeerUser && peerId > 0)
        {
            relatedUserIds.Add(peerId);
        }
        else
        {
            relatedChatIds.Add(peerId);
            AddSelectedMessagePeers(selected, relatedUserIds, relatedChatIds);
        }
        List<byte[]> relatedChatBytes = await _fanout.GetChatBytesForViewerAsync(userId,
            relatedChatIds);

        _log.Debug($"📜 {caller} user:{userId} peerType:{peerType} peer:{peerId} " +
                   $"matched:{conversation.Count} offsetId:{query.OffsetId} " +
                   $"addOffset:{query.AddOffset} startIndex:{startIndex} limit:{query.Limit} " +
                   $"-> {selected.Count} messages");
        return BuildMessages(userId, selected, relatedUserIds, relatedChatBytes);
    }

    public Task<TLMessages> BuildChannelMessagesAsync(long userId, long channelId,
        IReadOnlyList<MessageSnapshot> conversation, HistoryQuery query,
        int totalCount, string caller) =>
        BuildChannelResultAsync(userId, channelId, conversation, query, totalCount,
            caller, reportOffset: false);

    private async Task<TLMessages> BuildChannelResultAsync(long userId,
        long channelId, IReadOnlyList<MessageSnapshot> conversation,
        HistoryQuery query, int totalCount, string caller, bool reportOffset)
    {
        var channelBox = new ChannelMessageBox(_counterFactory, channelId);
        int channelPts = await channelBox.Pts();
        List<byte[]> selected = SelectHistory(conversation, query,
            allowUnlimited: true, out int startIndex);

        var relatedUserIds = new HashSet<long>();
        var relatedChatIds = new HashSet<long> { channelId };
        AddSelectedMessagePeers(selected, relatedUserIds, relatedChatIds);
        List<byte[]> relatedChatBytes = await _fanout.GetChatBytesForViewerAsync(userId,
            relatedChatIds);

        _log.Debug($"📜 {caller} user:{userId} channel:{channelId} pts:{channelPts} " +
                   $"total:{totalCount} offsetId:{query.OffsetId} addOffset:{query.AddOffset} " +
                   $"startIndex:{startIndex} limit:{query.Limit} -> {selected.Count} messages");
        return BuildChannelMessages(userId, channelPts, totalCount, selected, relatedUserIds,
            relatedChatBytes, reportOffset ? startIndex : null);
    }

    public async Task<TLMessages> BuildCommonSearchSliceAsync(long userId,
        TLPeer.PeerType peerType, long peerId, IReadOnlyList<MessageSnapshot> matched,
        HistoryQuery query, string caller)
    {
        List<byte[]> selected = SelectHistory(matched, query, allowUnlimited: false,
            out int startIndex);

        var relatedUserIds = new HashSet<long>();
        var relatedChatIds = new HashSet<long>();
        if (peerType == TLPeer.PeerType.PeerUser && peerId > 0)
        {
            relatedUserIds.Add(peerId);
        }
        else
        {
            relatedChatIds.Add(peerId);
        }
        AddSelectedMessagePeers(selected, relatedUserIds, relatedChatIds);
        List<byte[]> relatedChatBytes = await _fanout.GetChatBytesForViewerAsync(userId,
            relatedChatIds);

        _log.Debug($"🔎 {caller} user:{userId} peerType:{peerType} peer:{peerId} " +
                   $"matched:{matched.Count} offsetId:{query.OffsetId} " +
                   $"addOffset:{query.AddOffset} startIndex:{startIndex} " +
                   $"limit:{query.Limit} -> {selected.Count} messages");
        return BuildMessagesSlice(userId, matched.Count, startIndex, null, selected,
            relatedUserIds, relatedChatBytes);
    }

    public Task<TLMessages> BuildChannelSearchSliceAsync(long userId, long channelId,
        IReadOnlyList<MessageSnapshot> matched, HistoryQuery query, string caller) =>
        BuildChannelResultAsync(userId, channelId, matched, query, matched.Count,
            caller, reportOffset: true);

    public async Task<(HashSet<long> UserIds, List<byte[]> ChatBytes)>
        ResolveRelatedPeersAsync(long userId, TLPeer.PeerType peerType, long peerId,
            IReadOnlyList<byte[]> selected)
    {
        var relatedUserIds = new HashSet<long>();
        var relatedChatIds = new HashSet<long>();
        if (peerType == TLPeer.PeerType.PeerUser && peerId > 0)
        {
            relatedUserIds.Add(peerId);
        }
        else if (peerId > 0)
        {
            relatedChatIds.Add(peerId);
        }
        AddSelectedMessagePeers(selected, relatedUserIds, relatedChatIds);
        List<byte[]> relatedChatBytes = await _fanout.GetChatBytesForViewerAsync(userId,
            relatedChatIds);
        return (relatedUserIds, relatedChatBytes);
    }

    public async Task<TLMessages> BuildGlobalSearchSliceAsync(long userId,
        IReadOnlyList<byte[]> selected, int totalCount, int? nextRate, string caller)
    {
        var relatedUserIds = new HashSet<long>();
        var relatedChatIds = new HashSet<long>();
        AddSelectedMessagePeers(selected, relatedUserIds, relatedChatIds);
        List<byte[]> relatedChatBytes = await _fanout.GetChatBytesForViewerAsync(userId,
            relatedChatIds);

        _log.Debug($"🔎 {caller} user:{userId} total:{totalCount} " +
                   $"nextRate:{nextRate} -> {selected.Count} messages");
        return BuildMessagesSlice(userId, totalCount, null, nextRate, selected, relatedUserIds,
            relatedChatBytes);
    }

    public async Task<TLMessages> BuildPublicPostSearchSliceAsync(long userId,
        IReadOnlyList<byte[]> selected, int totalCount, int? nextRate,
        byte[] searchFlood, string caller)
    {
        var relatedUserIds = new HashSet<long>();
        var relatedChatIds = new HashSet<long>();
        AddSelectedMessagePeers(selected, relatedUserIds, relatedChatIds);
        List<byte[]> relatedChatBytes = await _fanout.GetChatBytesForViewerAsync(userId,
            relatedChatIds);

        _log.Debug($"🔎 {caller} user:{userId} total:{totalCount} " +
                   $"nextRate:{nextRate} -> {selected.Count} posts");
        return BuildMessagesSlice(userId, totalCount, null, nextRate, selected, relatedUserIds,
            relatedChatBytes, searchFlood);
    }

    public async Task<TLMessages> BuildSelectedMessagesAsync(long userId,
        TLPeer.PeerType peerType, long peerId, IReadOnlyList<byte[]> selected)
    {
        var relatedUserIds = new HashSet<long>();
        var relatedChatIds = new HashSet<long>();
        if (peerType == TLPeer.PeerType.PeerUser && peerId > 0)
        {
            relatedUserIds.Add(peerId);
        }
        else
        {
            relatedChatIds.Add(peerId);
        }
        AddSelectedMessagePeers(selected, relatedUserIds, relatedChatIds);
        List<byte[]> relatedChatBytes = await _fanout.GetChatBytesForViewerAsync(
            userId, relatedChatIds);

        var rows = selected.ToList();
        if (peerType != TLPeer.PeerType.PeerChannel)
        {
            return BuildMessages(userId, rows, relatedUserIds, relatedChatBytes);
        }

        var channelBox = new ChannelMessageBox(_counterFactory, peerId);
        int channelPts = await channelBox.Pts();
        return BuildChannelMessages(userId, channelPts, rows.Count, rows, relatedUserIds,
            relatedChatBytes);
    }

    public async Task<TLDialogs> GetDialogsAsync(long authKeyId, long userId,
        DialogQuery query)
    {
        IUpdatesContext userCtx = _updatesContextFactory.GetUpdatesContext(authKeyId,
            userId);
        IReadOnlyCollection<TLSavedMessage> saved = await _messageRepository
            .GetMessagesAsync(userId);
        int savedRows = saved.Count;
        var grouped = GroupDialogMessagesByPeer(saved);
        Dictionary<DialogPeerKey, MessageSnapshot> topMessages = grouped.TopMessages;
        int groupedPeers = topMessages.Count;
        Dictionary<DialogPeerKey, ChannelDialogInfo> channelDialogs =
            await GatherChannelDialogs(userId);
        foreach (var (key, info) in channelDialogs)
        {
            topMessages[key] = new MessageSnapshot(info.TopId, info.Date, info.TopBytes);
        }
        Dictionary<DialogPeerKey, DialogDraftSnapshot> drafts =
            await GatherDialogDrafts(userId);
        Dictionary<DialogPeerKey, DialogOrganizationState> organization =
            await DialogOrganizationStore.ReadPeerStatesAsync(
                _dialogOrganizationRepository, userId);

        List<DialogPeerKey> orderedPeers = OrderDialogPeers(topMessages, organization,
            query.FolderId);
        List<DialogPeerKey> pagedPeers = PageDialogPeers(orderedPeers, topMessages,
            query);
        Dictionary<DialogPeerKey, DialogState> dialogState = await GatherDialogState(
            userId, userCtx, pagedPeers, channelDialogs,
            grouped.UnreadMentionCounts, organization);
        Dictionary<DialogPeerKey, int> unreadReactionCounts =
            await GatherUnreadReactionCounts(userId);
        TLDialogs result = await BuildDialogsResult(userId, pagedPeers, topMessages,
            channelDialogs, dialogState, unreadReactionCounts, drafts);
        _log.Debug($"🗂 GetDialogs user:{userId} rows:{savedRows} " +
                   $"grouped:{groupedPeers} folder:{query.FolderId} " +
                   $"total:{orderedPeers.Count} " +
                   $"offsetDate:{query.OffsetDate} offsetId:{query.OffsetId} " +
                   $"limit:{query.Limit} -> {pagedPeers.Count} dialogs");
        return result;
    }

    public async Task<TLPeerDialogs> GetPeerDialogsAsync(long authKeyId, long userId,
        IReadOnlyList<DialogPeerKey> requested)
    {
        IUpdatesContext userCtx = _updatesContextFactory.GetUpdatesContext(authKeyId,
            userId);
        IReadOnlyCollection<TLSavedMessage> saved = await _messageRepository
            .GetMessagesAsync(userId);
        var grouped = GroupDialogMessagesByPeer(saved);
        Dictionary<DialogPeerKey, MessageSnapshot> topMessages = grouped.TopMessages;
        Dictionary<DialogPeerKey, ChannelDialogInfo> channelDialogs =
            await GatherChannelDialogs(userId);
        foreach (var (key, info) in channelDialogs)
        {
            topMessages[key] = new MessageSnapshot(info.TopId, info.Date, info.TopBytes);
        }
        Dictionary<DialogPeerKey, DialogDraftSnapshot> drafts =
            await GatherDialogDrafts(userId);
        Dictionary<DialogPeerKey, DialogOrganizationState> organization =
            await DialogOrganizationStore.ReadPeerStatesAsync(
                _dialogOrganizationRepository, userId);

        var previewChannelPts = new Dictionary<DialogPeerKey, int>();
        foreach (DialogPeerKey key in requested)
        {
            if (key.Type != TLPeer.PeerType.PeerChannel || channelDialogs.ContainsKey(key))
            {
                continue;
            }
            using TLChat? chat = await _chatRepository.GetChatAsync(key.Id);
            if (chat == null || chat.Value.Type != TLChat.ChatType.Channel)
            {
                continue;
            }
            previewChannelPts[key] = await new ChannelMessageBox(_counterFactory, key.Id)
                .Pts();
        }

        Dictionary<DialogPeerKey, DialogState> dialogState = await GatherDialogState(
            userId, userCtx, requested, channelDialogs,
            grouped.UnreadMentionCounts, organization);
        Dictionary<DialogPeerKey, int> unreadReactionCounts =
            await GatherUnreadReactionCounts(userId);
        using var state = await _updatesState.GetState(authKeyId);
        _log.Debug($"📂 GetPeerDialogs user:{userId} requested:{requested.Count} -> " +
                   $"{requested.Count} dialogs");

        var relatedUserIds = new HashSet<long>();
        var relatedChatIds = new HashSet<long>();
        foreach (DialogPeerKey peer in requested)
        {
            AddDialogPeerRelated(peer, relatedUserIds, relatedChatIds);
            if (topMessages.TryGetValue(peer, out MessageSnapshot? top))
            {
                using var message = new TLMessage(top.Bytes, 0, top.Bytes.Length);
                AddMessageRelatedPeers(message, relatedUserIds, relatedChatIds);
            }
        }
        List<byte[]> relatedChatBytes = await _fanout.GetChatBytesForViewerAsync(userId,
            relatedChatIds);
        Dictionary<DialogPeerKey, int> ttlPeriods = await ResolveTtlPeriodsAsync(userId,
            requested);
        var dialogs = new Vector();
        var messages = new Vector();
        foreach (DialogPeerKey peer in requested)
        {
            int channelPts = channelDialogs.TryGetValue(peer, out ChannelDialogInfo? info)
                ? info.Pts
                : previewChannelPts.GetValueOrDefault(peer);
            if (topMessages.TryGetValue(peer, out MessageSnapshot? top))
            {
                AppendDialog(ref dialogs, peer, top.Id, dialogState[peer], channelPts,
                    unreadReactionCounts.GetValueOrDefault(peer),
                    ttlPeriods.GetValueOrDefault(peer),
                    drafts.GetValueOrDefault(peer)?.Bytes);
                if (top.Bytes.Length > 0) messages.AppendTLObject(top.Bytes);
            }
            else
            {
                AppendDialog(ref dialogs, peer, 0, dialogState[peer], channelPts,
                    unreadReactionCounts.GetValueOrDefault(peer),
                    ttlPeriods.GetValueOrDefault(peer),
                    drafts.GetValueOrDefault(peer)?.Bytes);
            }
        }
        var users = new Vector();
        _fanout.AppendUsers(userId, ref users, relatedUserIds);
        var chats = BuildVector(relatedChatBytes);
        return PeerDialogs.Builder().Dialogs(dialogs).Messages(messages).Chats(chats)
            .Users(users).State(state.AsSpan()).Build();
    }

    internal static Dictionary<DialogPeerKey, MessageSnapshot> GroupTopMessagesByPeer(
        IReadOnlyCollection<TLSavedMessage> saved) =>
        GroupDialogMessagesByPeer(saved).TopMessages;

    private static (Dictionary<DialogPeerKey, MessageSnapshot> TopMessages,
        Dictionary<DialogPeerKey, int> UnreadMentionCounts)
        GroupDialogMessagesByPeer(IReadOnlyCollection<TLSavedMessage> saved)
    {
        var top = new Dictionary<DialogPeerKey, MessageSnapshot>();
        var unreadMentions = new Dictionary<DialogPeerKey, int>();
        foreach (TLSavedMessage value in saved)
        {
            using var savedMessage = value;
            TLMessage message = savedMessage.AsSavedMessage().Get_OriginalMessage();
            if (!MessageStore.TryReadStoredMessageInfo(message, out StoredMessageInfo info) ||
                info.PeerType is not (TLPeer.PeerType.PeerUser or
                    TLPeer.PeerType.PeerChat))
            {
                continue;
            }
            var key = new DialogPeerKey(info.PeerType, info.PeerId);
            if (MentionScope.IsUnreadCommonMention(info.Bytes, info.Id, topMsgId: 0))
            {
                unreadMentions[key] = unreadMentions.GetValueOrDefault(key) + 1;
            }
            if (!top.TryGetValue(key, out MessageSnapshot? current) || info.Id > current.Id)
            {
                top[key] = new MessageSnapshot(info.Id, info.Date, info.Bytes);
            }
        }
        return (top, unreadMentions);
    }

    public static List<DialogPeerKey> OrderDialogPeers(
        Dictionary<DialogPeerKey, MessageSnapshot> topMessages) => topMessages
        .OrderByDescending(kv => kv.Value.Date)
        .ThenByDescending(kv => kv.Value.Id)
        .ThenByDescending(kv => (int)kv.Key.Type)
        .ThenByDescending(kv => kv.Key.Id)
        .Select(kv => kv.Key)
        .ToList();

    public static List<DialogPeerKey> OrderDialogPeers(
        Dictionary<DialogPeerKey, MessageSnapshot> topMessages,
        IReadOnlyDictionary<DialogPeerKey, DialogOrganizationState> organization,
        int folderId) => topMessages
        .Where(kv => organization.GetValueOrDefault(kv.Key,
            DialogOrganizationState.Default).FolderId == folderId)
        .OrderByDescending(kv => organization.GetValueOrDefault(kv.Key,
            DialogOrganizationState.Default).Pinned)
        .ThenByDescending(kv => organization.GetValueOrDefault(kv.Key,
            DialogOrganizationState.Default).PinOrder)
        .ThenByDescending(kv => kv.Value.Date)
        .ThenByDescending(kv => kv.Value.Id)
        .ThenByDescending(kv => (int)kv.Key.Type)
        .ThenByDescending(kv => kv.Key.Id)
        .Select(kv => kv.Key)
        .ToList();

    public static List<DialogPeerKey> PageDialogPeers(
        IReadOnlyList<DialogPeerKey> orderedPeers,
        Dictionary<DialogPeerKey, MessageSnapshot> topMessages, DialogQuery query)
    {
        int startIndex = FindDialogPageStart(orderedPeers, topMessages, query.OffsetDate,
            query.OffsetId, query.OffsetPeer);
        return orderedPeers.Skip(startIndex).Take(query.Limit).ToList();
    }

    public static int FindDialogPageStart(IReadOnlyList<DialogPeerKey> orderedPeers,
        Dictionary<DialogPeerKey, MessageSnapshot> topMessages, int offsetDate,
        int offsetId, DialogPeerKey? offsetPeer)
    {
        if (offsetPeer is { } peer)
        {
            int index = -1;
            for (int i = 0; i < orderedPeers.Count; i++)
            {
                if (orderedPeers[i] == peer)
                {
                    index = i;
                    break;
                }
            }
            if (index >= 0) return index + 1;
        }
        if (offsetDate <= 0 && offsetId <= 0) return 0;
        for (int i = 0; i < orderedPeers.Count; i++)
        {
            MessageSnapshot top = topMessages[orderedPeers[i]];
            bool olderDate = offsetDate > 0 && top.Date < offsetDate;
            bool sameDateOlderId = offsetDate > 0 && top.Date == offsetDate &&
                                   offsetId > 0 && top.Id < offsetId;
            bool olderIdOnly = offsetDate <= 0 && offsetId > 0 && top.Id < offsetId;
            if (olderDate || sameDateOlderId || olderIdOnly) return i;
        }
        return orderedPeers.Count;
    }

    private async Task<Dictionary<DialogPeerKey, DialogState>> GatherDialogState(
        long userId, IUpdatesContext userCtx, IEnumerable<DialogPeerKey> peers,
        IReadOnlyDictionary<DialogPeerKey, ChannelDialogInfo> channelDialogs,
        IReadOnlyDictionary<DialogPeerKey, int> commonUnreadMentionCounts,
        IReadOnlyDictionary<DialogPeerKey, DialogOrganizationState> organization)
    {
        var result = new Dictionary<DialogPeerKey, DialogState>();
        foreach (DialogPeerKey peer in peers)
        {
            DialogOrganizationState organized = organization.GetValueOrDefault(peer,
                DialogOrganizationState.Default);
            if (peer.Type == TLPeer.PeerType.PeerChannel &&
                channelDialogs.TryGetValue(peer, out ChannelDialogInfo? channel))
            {
                result[peer] = new DialogState(channel.Unread, channel.ReadInbox,
                    channel.ReadOutbox, channel.UnreadMentions, organized.FolderId,
                    organized.Pinned, organized.UnreadMark, organized.PinOrder);
                continue;
            }
            int unread = await userCtx.UnreadMessages((int)peer.Type, peer.Id);
            int readInbox = await userCtx.ReadMessagesMaxId((int)peer.Type, peer.Id);
            int readOutbox = 0;
            if (peer.Type == TLPeer.PeerType.PeerUser)
            {
                IUpdatesContext peerCtx = _updatesContextFactory.GetUpdatesContext(null,
                    peer.Id);
                readOutbox = await peerCtx.ReadMessagesMaxId(
                    (int)TLPeer.PeerType.PeerUser, userId);
            }
            result[peer] = new DialogState(unread, readInbox, readOutbox,
                commonUnreadMentionCounts.GetValueOrDefault(peer), organized.FolderId,
                organized.Pinned, organized.UnreadMark, organized.PinOrder);
        }
        return result;
    }

    private async Task<Dictionary<DialogPeerKey, ChannelDialogInfo>>
        GatherChannelDialogs(long userId)
    {
        var result = new Dictionary<DialogPeerKey, ChannelDialogInfo>();
        IReadOnlyCollection<TLChatParticipantInfo> participations = await _chatParticipantsRepository.GetParticipantsByUserAsync(userId);
        foreach (TLChatParticipantInfo participation in participations)
        {
            using var participant = participation;
            if (!IsActiveParticipant(participant)) continue;
            long channelId = participant.AsChatParticipantInfo().ChatId;
            using TLChat? chat = await _chatRepository.GetChatAsync(channelId);
            if (chat == null || chat.Value.Type != TLChat.ChatType.Channel) continue;

            int readInbox = 0;
            int readOutbox = 0;
            using (TLChannelReadState? readState = await _channelMessagesRepository.GetReadStateAsync(userId, channelId))
            {
                if (readState != null)
                {
                    var state = readState.Value.AsChannelReadState();
                    readInbox = state.ReadInboxMaxId;
                    readOutbox = state.ReadOutboxMaxId;
                }
            }
            IReadOnlyCollection<TLSavedMessage> messages = await _channelMessagesRepository.GetMessagesAsync(channelId, 0, 0);
            int topId = 0;
            int topDate = 0;
            byte[] topBytes = Array.Empty<byte>();
            int unread = 0;
            var posts = new List<MessageSnapshot>(messages.Count);
            foreach (TLSavedMessage saved in messages)
            {
                using var s = saved;
                TLMessage message = s.AsSavedMessage().Get_OriginalMessage();
                if (!MessageStore.TryReadStoredMessageInfo(message,
                        out StoredMessageInfo info)) continue;
                posts.Add(new MessageSnapshot(info.Id, info.Date, info.Bytes));
                if (topBytes.Length == 0 || info.Id > topId)
                {
                    topId = info.Id;
                    topDate = info.Date;
                    topBytes = info.Bytes;
                }
                if (info.Id > readInbox && ResolveChannelPostSenderId(message.AsSpan()) != userId)
                    unread++;
            }
            int unreadMentions = (await _mentions.SelectUnreadChannelMentionsAsync(
                channelId, userId, posts, topMsgId: 0)).Count;
            int pts = await new ChannelMessageBox(_counterFactory, channelId).Pts();
            result[new DialogPeerKey(TLPeer.PeerType.PeerChannel, channelId)] =
                new ChannelDialogInfo(topId, topDate, topBytes, pts, unread, readInbox,
                    readOutbox, unreadMentions);
        }
        return result;
    }

    private async Task<Dictionary<DialogPeerKey, int>> GatherUnreadReactionCounts(
        long userId)
    {
        var counts = new Dictionary<DialogPeerKey, int>();
        IReadOnlyCollection<TLMessageReactionInfo> rows = await _messageReactionsRepository.GetBoxReactionsAsync(MessageReactionBox.Common,
                userId);
        foreach (TLMessageReactionInfo row in rows)
        {
            using var reaction = row;
            var info = reaction.AsMessageReactionInfo();
            if (!info.Unread) continue;
            var key = new DialogPeerKey((TLPeer.PeerType)info.PeerType, info.PeerId);
            counts[key] = counts.GetValueOrDefault(key) + 1;
        }
        return counts;
    }

    private async Task<TLDialogs> BuildDialogsResult(long userId,
        IReadOnlyList<DialogPeerKey> peers,
        Dictionary<DialogPeerKey, MessageSnapshot> topMessages,
        Dictionary<DialogPeerKey, ChannelDialogInfo> channelDialogs,
        Dictionary<DialogPeerKey, DialogState> dialogState,
        Dictionary<DialogPeerKey, int> unreadReactionCounts,
        Dictionary<DialogPeerKey, DialogDraftSnapshot> drafts)
    {
        var relatedUserIds = new HashSet<long>();
        var relatedChatIds = new HashSet<long>();
        foreach (DialogPeerKey peer in peers)
        {
            AddDialogPeerRelated(peer, relatedUserIds, relatedChatIds);
            MessageSnapshot top = topMessages[peer];
            if (top.Bytes.Length > 0)
            {
                using var message = new TLMessage(top.Bytes, 0, top.Bytes.Length);
                AddMessageRelatedPeers(message, relatedUserIds, relatedChatIds);
            }
        }
        List<byte[]> chatBytes = await _fanout.GetChatBytesForViewerAsync(userId,
            relatedChatIds);
        Dictionary<DialogPeerKey, int> ttlPeriods = await ResolveTtlPeriodsAsync(userId,
            peers);
        var dialogs = new Vector();
        var messages = new Vector();
        foreach (DialogPeerKey peer in peers)
        {
            MessageSnapshot top = topMessages[peer];
            int channelPts = channelDialogs.TryGetValue(peer, out ChannelDialogInfo? info)
                ? info.Pts : 0;
            AppendDialog(ref dialogs, peer, top.Id, dialogState[peer], channelPts,
                unreadReactionCounts.GetValueOrDefault(peer),
                ttlPeriods.GetValueOrDefault(peer),
                drafts.GetValueOrDefault(peer)?.Bytes);
            if (top.Bytes.Length > 0) messages.AppendTLObject(top.Bytes);
        }
        var users = new Vector();
        _fanout.AppendUsers(userId, ref users, relatedUserIds);
        var chats = BuildVector(chatBytes);
        return Ferrite.TL.baseLayer.messages.Dialogs.Builder()
            .DialogsProperty(dialogs).Messages(messages).Chats(chats)
            .Users(users).Build();
    }

    private static List<MessageSnapshot> ReadConversation(
        IReadOnlyCollection<TLSavedMessage> saved, DialogPeerKey? filter)
    {
        var result = new List<MessageSnapshot>();
        foreach (TLSavedMessage value in saved)
        {
            using var savedMessage = value;
            TLMessage message = savedMessage.AsSavedMessage().Get_OriginalMessage();
            if (!MessageStore.TryReadStoredMessageInfo(message, out StoredMessageInfo info) ||
                filter is { } peer && (info.PeerType != peer.Type || info.PeerId != peer.Id))
                continue;
            result.Add(new MessageSnapshot(info.Id, info.Date, info.Bytes));
        }
        return result;
    }

    private static List<byte[]> SelectHistory(IReadOnlyList<MessageSnapshot> conversation,
        HistoryQuery query, bool allowUnlimited, out int startIndex)
    {
        int baseIndex = query.OffsetId > 0
            ? conversation.Count(m => m.Id >= query.OffsetId)
            : query.OffsetDate > 0
                ? conversation.Count(m => m.Date >= query.OffsetDate)
                : 0;
        startIndex = Math.Clamp(baseIndex + query.AddOffset, 0, conversation.Count);
        var selected = new List<byte[]>();
        for (int i = startIndex; i < conversation.Count &&
             ((allowUnlimited && query.Limit <= 0) || selected.Count < query.Limit); i++)
        {
            MessageSnapshot message = conversation[i];
            if (query.MaxId > 0 && message.Id > query.MaxId) continue;
            if (query.MinId > 0 && message.Id < query.MinId) continue;
            selected.Add(message.Bytes);
        }
        return selected;
    }

    private TLMessages BuildMessages(long viewerUserId, List<byte[]> messageBytes,
        IEnumerable<long> userIds, IReadOnlyCollection<byte[]> chatBytes)
    {
        var messages = BuildVector(messageBytes);
        var users = new Vector();
        _fanout.AppendUsers(viewerUserId, ref users, userIds);
        var chats = BuildVector(chatBytes);
        return Ferrite.TL.baseLayer.messages.Messages.Builder()
            .MessagesProperty(messages).Chats(chats).Users(users)
            .Build();
    }

    private TLMessages BuildChannelMessages(long viewerUserId, int pts, int totalCount,
        List<byte[]> messageBytes, IEnumerable<long> userIds,
        IReadOnlyCollection<byte[]> chatBytes, int? offsetIdOffset = null)
    {
        var messages = BuildVector(messageBytes);
        var users = new Vector();
        _fanout.AppendUsers(viewerUserId, ref users, userIds);
        var chats = BuildVector(chatBytes);
        var builder = ChannelMessages.Builder().Pts(pts).Count(totalCount);
        if (offsetIdOffset is { } offset)
        {
            builder = builder.OffsetIdOffset(offset);
        }
        return builder.Messages(messages).Topics(new Vector()).Chats(chats)
            .Users(users).Build();
    }

    private TLMessages BuildMessagesSlice(long viewerUserId, int totalCount, int? offsetIdOffset,
        int? nextRate, IReadOnlyList<byte[]> messageBytes, IEnumerable<long> userIds,
        IReadOnlyCollection<byte[]> chatBytes, byte[]? searchFlood = null)
    {
        var messages = BuildVector(messageBytes);
        var users = new Vector();
        _fanout.AppendUsers(viewerUserId, ref users, userIds);
        var chats = BuildVector(chatBytes);
        var builder = MessagesSlice.Builder().Count(totalCount);
        if (nextRate is { } rate)
        {
            builder = builder.NextRate(rate);
        }
        if (offsetIdOffset is { } offset)
        {
            builder = builder.OffsetIdOffset(offset);
        }
        if (searchFlood != null)
        {
            builder = builder.SearchFlood(searchFlood);
        }
        return builder.Messages(messages).Chats(chats).Users(users).Build();
    }

    private static Vector BuildVector(IEnumerable<byte[]> values)
    {
        var vector = new Vector();
        foreach (byte[] value in values) vector.AppendTLObject(value);
        return vector;
    }

    private static void AddSelectedMessagePeers(IEnumerable<byte[]> selected,
        HashSet<long> userIds, HashSet<long> chatIds)
    {
        foreach (byte[] bytes in selected)
        {
            using var message = new TLMessage(bytes, 0, bytes.Length);
            AddMessageRelatedPeers(message, userIds, chatIds);
        }
    }

    private static void AddMessageRelatedPeers(TLMessage message, HashSet<long> userIds,
        HashSet<long> chatIds)
    {
        if (!MessageStore.TryReadStoredMessageInfo(message, out StoredMessageInfo info)) return;
        if (info.PeerType == TLPeer.PeerType.PeerUser) userIds.Add(info.PeerId);
        else if (info.PeerType is TLPeer.PeerType.PeerChat or TLPeer.PeerType.PeerChannel)
            chatIds.Add(info.PeerId);

        if (message.Type == TLMessage.MessageType.Message)
        {
            var regular = message.AsMessage();
            if (regular.Flags[8] && TryReadPeer(regular.Get_FromIdView(), out var from))
            {
                if (from.Type == TLPeer.PeerType.PeerUser)
                {
                    userIds.Add(from.Id);
                }
                else
                {
                    chatIds.Add(from.Id);
                }
            }
            return;
        }
        if (message.Type != TLMessage.MessageType.MessageService) return;
        var service = message.AsMessageService();
        if (service.Flags[8] && TryReadPeer(service.Get_FromIdView(),
                out var serviceFrom))
        {
            if (serviceFrom.Type == TLPeer.PeerType.PeerUser)
            {
                userIds.Add(serviceFrom.Id);
            }
            else
            {
                chatIds.Add(serviceFrom.Id);
            }
        }
        MessageActionView action = service.Get_ActionView();
        foreach (long userId in ReadActionUserIds(action)) userIds.Add(userId);
        foreach (long chatId in ReadActionChatIds(action)) chatIds.Add(chatId);
    }

    private static List<long> ReadActionUserIds(MessageActionView action)
    {
        var result = new List<long>();
        if (action.Is(out MessageActionChatCreate create))
        {
            var users = create.Users;
            for (int i = 0; i < users.Count; i++) result.Add(users[i]);
        }
        else if (action.Is(out MessageActionChatAddUser add))
        {
            var users = add.Users;
            for (int i = 0; i < users.Count; i++) result.Add(users[i]);
        }
        else if (action.Is(out MessageActionChatDeleteUser deleteUser))
            result.Add(deleteUser.UserId);
        else if (action.Is(out MessageActionChatJoinedByLink joined))
            result.Add(joined.InviterId);
        return result;
    }

    private static List<long> ReadActionChatIds(MessageActionView action)
    {
        var result = new List<long>();
        if (action.Is(out MessageActionChatMigrateTo migrateTo))
            result.Add(migrateTo.ChannelId);
        else if (action.Is(out MessageActionChannelMigrateFrom migrateFrom))
            result.Add(migrateFrom.ChatId);
        return result;
    }

    private static bool TryReadPeer(PeerView peer,
        out (TLPeer.PeerType Type, long Id) value)
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

    private static void AddDialogPeerRelated(DialogPeerKey peer, HashSet<long> userIds,
        HashSet<long> chatIds)
    {
        if (peer.Type == TLPeer.PeerType.PeerUser) userIds.Add(peer.Id);
        else if (peer.Type is TLPeer.PeerType.PeerChat or TLPeer.PeerType.PeerChannel)
            chatIds.Add(peer.Id);
    }

    private static void AppendDialog(ref Vector dialogs, DialogPeerKey peerKey,
        int topMessageId, DialogState state, int channelPts, int unreadReactionsCount,
        int ttlPeriod = 0, byte[]? draftBytes = null)
    {
        using TLPeer peer = PeerResolver.BuildPeer(peerKey.Type, peerKey.Id);
        using var notifySettings = PeerNotifySettings.Builder().Build();
        var builder = Dialog.Builder().Peer(peer.AsSpan()).TopMessage(topMessageId)
            .ReadInboxMaxId(state.ReadInbox).ReadOutboxMaxId(state.ReadOutbox)
            .UnreadCount(state.Unread).UnreadMentionsCount(state.UnreadMentions)
            .UnreadReactionsCount(unreadReactionsCount)
            .NotifySettings(notifySettings.ToReadOnlySpan());
        if (peerKey.Type == TLPeer.PeerType.PeerChannel) builder = builder.Pts(channelPts);
        if (state.Pinned) builder = builder.Pinned(true);
        if (state.UnreadMark) builder = builder.UnreadMark(true);
        if (state.FolderId != 0) builder = builder.FolderId(state.FolderId);
        if (draftBytes is { Length: > 0 }) builder = builder.Draft(draftBytes);
        if (ttlPeriod > 0) builder = builder.TtlPeriod(ttlPeriod);
        using var dialog = builder.Build();
        dialogs.AppendTLObject(dialog.ToReadOnlySpan());
    }

    private async Task<Dictionary<DialogPeerKey, DialogDraftSnapshot>>
        GatherDialogDrafts(long userId)
    {
        IReadOnlyCollection<TLDraftInfo> rows = await _draftsRepository
            .GetDraftsAsync(userId);
        var drafts = new Dictionary<DialogPeerKey, DialogDraftSnapshot>();
        foreach (TLDraftInfo row in rows)
        {
            using var owned = row;
            var info = owned.AsDraftInfo();
            if (info.TopMsgId != 0 ||
                !info.Get_DraftView().Is(out DraftMessage draft))
            {
                continue;
            }
            var key = new DialogPeerKey((TLPeer.PeerType)info.PeerType,
                info.PeerId);
            drafts[key] = new DialogDraftSnapshot(info.Draft.ToArray(), draft.Date);
        }
        return drafts;
    }

    private async Task<Dictionary<DialogPeerKey, int>> ResolveTtlPeriodsAsync(
        long userId, IEnumerable<DialogPeerKey> peers)
    {
        var periods = new Dictionary<DialogPeerKey, int>();
        foreach (DialogPeerKey peer in peers)
        {
            if (periods.ContainsKey(peer))
            {
                continue;
            }
            periods[peer] = await _expiry.ResolveTtlPeriodAsync(userId, peer.Type,
                peer.Id);
        }
        return periods;
    }

    private static long ResolveChannelPostSenderId(Span<byte> messageSpan)
    {
        var message = (Message)messageSpan;
        if (message.Constructor != Constructors.baseLayer_Message) return 0;
        return message.Get_FromIdView().Is(out PeerUser user) ? user.UserId : 0;
    }

    private static bool IsActiveParticipant(TLChatParticipantInfo participant)
    {
        int role = participant.AsChatParticipantInfo().Role;
        return role is not ((int)ChatParticipantRole.Banned) and
            not ((int)ChatParticipantRole.Left);
    }
}

public sealed record MessageSnapshot(int Id, int Date, byte[] Bytes);

public sealed record BoxMessage(MessageSnapshot Snapshot, TLPeer.PeerType PeerType,
    long PeerId);
internal sealed record DialogState(int Unread, int ReadInbox, int ReadOutbox,
    int UnreadMentions, int FolderId, bool Pinned, bool UnreadMark, int PinOrder);
internal sealed record ChannelDialogInfo(int TopId, int Date, byte[] TopBytes, int Pts,
    int Unread, int ReadInbox, int ReadOutbox, int UnreadMentions);
internal sealed record DialogDraftSnapshot(byte[] Bytes, int Date);
