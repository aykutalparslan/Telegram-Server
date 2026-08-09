// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services;

// Await-safe snapshot of one reactor's stored dto.messageReactionInfo row.
public sealed record ReactionEntry(long UserId, bool Big, bool Unread, int Date,
    long Order, List<byte[]> Reactions);

// Reaction persistence + parsing: per-chat available-reactions config, per-user
// reaction settings, and the stored messageReactionInfo row helpers. The pure
// parse/validate/row builders stay static; the config/settings methods read and
// write through IUnitOfWork. Extracted verbatim from MessagesService (dispatch
// refactor P3).
public sealed class ReactionStore
{
    private readonly IChannelMessagesRepository _channelMessagesRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IMessageReactionsRepository _messageReactionsRepository;
    private readonly IMessageRepository _messageRepository;

    private const int ChatReactionsModeAll = 0;
    private const int ChatReactionsModeNone = 1;
    private const int ChatReactionsModeSome = 2;

    private readonly IUnitOfWork _unitOfWork;

    public ReactionStore(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IChatRepository chatRepository, IMessageReactionsRepository messageReactionsRepository, IMessageRepository messageRepository)
    {
        _channelMessagesRepository = channelMessagesRepository;
        _chatRepository = chatRepository;
        _messageReactionsRepository = messageReactionsRepository;
        _messageRepository = messageRepository;

        _unitOfWork = unitOfWork;
    }

    public async Task<(byte[]? Available, int Limit)> GetStoredChatReactionConfigAsync(
        long chatId)
    {
        using var storedFullInfo = await _chatRepository.GetFullInfoAsync(chatId);
        if (storedFullInfo == null)
        {
            return (null, 0);
        }

        var info = storedFullInfo.Value.AsChatFullInfo();
        byte[]? available = info.Flags[2] ? info.AvailableReactions.ToArray() : null;
        return (available, info.ReactionsLimit);
    }

    public async Task PutChatReactionConfig(long chatId, byte[] availableBytes,
        int? reactionsLimit)
    {
        using var storedFullInfo = await _chatRepository.GetFullInfoAsync(chatId);
        if (storedFullInfo == null)
        {
            var createdBuilder = ChatFullInfo.Builder()
                .ChatId(chatId)
                .About(ReadOnlySpan<byte>.Empty)
                .AvailableReactions(availableBytes);
            if (reactionsLimit is > 0)
            {
                createdBuilder = createdBuilder.ReactionsLimit(reactionsLimit.Value);
            }
            using TLChatFullInfo created = createdBuilder.Build();
            _chatRepository.PutFullInfo(created);
            return;
        }

        var fullInfo = storedFullInfo.Value.AsChatFullInfo();
        var builder = ChatFullInfo.Builder()
            .ChatId(fullInfo.ChatId)
            .About(fullInfo.About)
            .AvailableReactions(availableBytes);
        if (fullInfo.Flags[0])
        {
            builder = builder.PinnedMsgId(fullInfo.PinnedMsgId);
        }
        if (fullInfo.Flags[1])
        {
            builder = builder.DefaultBannedRights(fullInfo.DefaultBannedRights);
        }
        int limit = reactionsLimit ?? (fullInfo.Flags[3] ? fullInfo.ReactionsLimit : 0);
        if (limit > 0)
        {
            builder = builder.ReactionsLimit(limit);
        }
        if (fullInfo.ForumTabs)
        {
            builder = builder.ForumTabs(true);
        }
        if (fullInfo.Flags[5])
        {
            builder = builder
                .MigratedFromChatId(fullInfo.MigratedFromChatId)
                .MigratedFromMaxId(fullInfo.MigratedFromMaxId);
        }
        using TLChatFullInfo updated = builder.Build();
        _chatRepository.PutFullInfo(updated);
    }

    private static (int Mode, List<byte[]> Allowed) ParseChatReactions(byte[]? availableBytes)
    {
        if (availableBytes == null)
        {
            return (ChatReactionsModeAll, new List<byte[]>());
        }

        var view = (ChatReactionsAll)availableBytes.AsSpan();
        if (view.Constructor == Constructors.baseLayer_ChatReactionsNone)
        {
            return (ChatReactionsModeNone, new List<byte[]>());
        }
        if (view.Constructor == Constructors.baseLayer_ChatReactionsSome)
        {
            var some = (ChatReactionsSome)availableBytes.AsSpan();
            var allowed = new List<byte[]>();
            var vector = some.Reactions;
            int count = vector.Count;
            for (int i = 0; i < count; i++)
            {
                allowed.Add(vector.ReadTLObject().ToArray());
            }
            return (ChatReactionsModeSome, allowed);
        }

        return (ChatReactionsModeAll, new List<byte[]>());
    }

    // Ferrite serves emoji reactions only; custom-emoji reactions are premium surface
    // and rejected as REACTION_INVALID.
    public static bool AreReactionsAllowed(List<byte[]> requested, byte[]? availableBytes)
    {
        var (mode, allowed) = ParseChatReactions(availableBytes);
        foreach (byte[] reactionBytes in requested)
        {
            var emoji = (ReactionEmoji)reactionBytes.AsSpan();
            if (emoji.Constructor != Constructors.baseLayer_ReactionEmoji)
            {
                return false;
            }

            switch (mode)
            {
                case ChatReactionsModeNone:
                    return false;
                case ChatReactionsModeSome:
                    if (!allowed.Any(a => a.AsSpan().SequenceEqual(reactionBytes)))
                    {
                        return false;
                    }
                    break;
                default:
                    if (!DefaultReactions.IsDefaultEmoji(emoji.Emoticon))
                    {
                        return false;
                    }
                    break;
            }
        }

        return true;
    }

    // Validates a client-provided ChatReactions value: All/None pass through; a Some
    // list must contain only default-set emoji reactions.
    public static bool IsValidChatReactionsValue(byte[] availableBytes)
    {
        var view = (ChatReactionsAll)availableBytes.AsSpan();
        if (view.Constructor is Constructors.baseLayer_ChatReactionsAll
            or Constructors.baseLayer_ChatReactionsNone)
        {
            return true;
        }
        if (view.Constructor != Constructors.baseLayer_ChatReactionsSome)
        {
            return false;
        }

        var some = (ChatReactionsSome)availableBytes.AsSpan();
        var vector = some.Reactions;
        int count = vector.Count;
        for (int i = 0; i < count; i++)
        {
            var element = vector.ReadTLObject();
            var emoji = (ReactionEmoji)element;
            if (emoji.Constructor != Constructors.baseLayer_ReactionEmoji ||
                !DefaultReactions.IsDefaultEmoji(emoji.Emoticon))
            {
                return false;
            }
        }

        return true;
    }

    public static List<ReactionEntry> ReadReactionEntries(
        IReadOnlyCollection<TLMessageReactionInfo> rows, long excludeUserId)
    {
        var entries = new List<ReactionEntry>();
        foreach (var row in rows)
        {
            using var reactionRow = row;
            var info = reactionRow.AsMessageReactionInfo();
            if (excludeUserId != 0 && info.UserId == excludeUserId)
            {
                continue;
            }

            var reactions = new List<byte[]>();
            var vector = info.Reactions;
            int count = vector.Count;
            for (int i = 0; i < count; i++)
            {
                reactions.Add(vector.ReadTLObject().ToArray());
            }
            entries.Add(new ReactionEntry(info.UserId, info.Big, info.Unread, info.Date,
                info.Order, reactions));
        }

        return entries;
    }

    public static List<ReactionEntry> MergeCallerEntry(List<ReactionEntry> otherEntries,
        long userId, List<byte[]> requested, bool big, bool unread, int date, long order)
    {
        var merged = new List<ReactionEntry>(otherEntries);
        if (requested.Count > 0)
        {
            merged.Add(new ReactionEntry(userId, big, unread, date, order, requested));
        }

        return merged;
    }

    public static bool ExceedsUniqueLimit(List<ReactionEntry> otherEntries,
        List<byte[]> requested, int uniqueLimit)
    {
        var unique = new HashSet<string>();
        foreach (var entry in otherEntries)
        {
            foreach (byte[] reaction in entry.Reactions)
            {
                unique.Add(Convert.ToHexString(reaction));
            }
        }
        foreach (byte[] reaction in requested)
        {
            unique.Add(Convert.ToHexString(reaction));
        }

        return unique.Count > uniqueLimit;
    }

    public static long ResolveChannelPostAuthorId(TLMessage original)
    {
        var message = original.AsMessage();
        if (!message.Flags[8])
        {
            return 0;
        }

        return PeerResolver.TryReadPeer(message.Get_FromIdView(), out var fromPeer) &&
               fromPeer.Type == TLPeer.PeerType.PeerUser
            ? fromPeer.Id
            : 0;
    }

    // Builds a serialized messageReactions value for one viewer. chosen_order marks the
    // viewer's own reactions in their sent order; recent_reactions carries the newest
    // reactor rows for group-style peers. A viewerId of 0 builds the neutral value
    // stored on the shared channel row.
    public static byte[] BuildMessageReactionsValue(List<ReactionEntry> entries,
        long viewerId, bool includeRecent, bool canSeeList, bool includeUnread)
    {
        var index = new Dictionary<string, int>();
        var counts = new List<(byte[] Reaction, int Count, long MinOrder, int ChosenOrder)>();
        foreach (var entry in entries)
        {
            for (int i = 0; i < entry.Reactions.Count; i++)
            {
                byte[] reaction = entry.Reactions[i];
                string key = Convert.ToHexString(reaction);
                if (!index.TryGetValue(key, out int position))
                {
                    position = counts.Count;
                    index[key] = position;
                    counts.Add((reaction, 0, entry.Order, -1));
                }

                var item = counts[position];
                item.Count++;
                if (entry.Order < item.MinOrder)
                {
                    item.MinOrder = entry.Order;
                }
                if (viewerId != 0 && entry.UserId == viewerId)
                {
                    item.ChosenOrder = i;
                }
                counts[position] = item;
            }
        }

        counts.Sort((a, b) => a.Count != b.Count
            ? b.Count.CompareTo(a.Count)
            : a.MinOrder.CompareTo(b.MinOrder));

        var results = new Vector();
        foreach (var item in counts)
        {
            var countBuilder = ReactionCount.Builder()
                .Reaction(item.Reaction)
                .Count(item.Count);
            if (item.ChosenOrder >= 0)
            {
                countBuilder = countBuilder.ChosenOrder(item.ChosenOrder);
            }
            using var reactionCount = countBuilder.Build();
            results.AppendTLObject(reactionCount.ToReadOnlySpan());
        }

        var reactionsBuilder = MessageReactions.Builder().Results(results);
        if (canSeeList)
        {
            reactionsBuilder = reactionsBuilder.CanSeeList(true);
        }
        if (includeRecent && entries.Count > 0)
        {
            var flattened = new List<(ReactionEntry Entry, byte[] Reaction, int Position)>();
            foreach (var entry in entries)
            {
                for (int i = 0; i < entry.Reactions.Count; i++)
                {
                    flattened.Add((entry, entry.Reactions[i], i));
                }
            }
            flattened.Sort((a, b) => a.Entry.Order != b.Entry.Order
                ? b.Entry.Order.CompareTo(a.Entry.Order)
                : b.Position.CompareTo(a.Position));

            var recent = new Vector();
            foreach (var (entry, reaction, _) in flattened.Take(3))
            {
                using TLPeer reactorPeer = new PeerUser(entry.UserId);
                var recentBuilder = MessagePeerReaction.Builder()
                    .PeerId(reactorPeer.AsSpan())
                    .Date(entry.Date)
                    .Reaction(reaction);
                if (entry.Big)
                {
                    recentBuilder = recentBuilder.Big(true);
                }
                if (includeUnread && entry.Unread)
                {
                    recentBuilder = recentBuilder.Unread(true);
                }
                if (viewerId != 0 && entry.UserId == viewerId)
                {
                    recentBuilder = recentBuilder.My(true);
                }
                using var peerReaction = recentBuilder.Build();
                recent.AppendTLObject(peerReaction.ToReadOnlySpan());
            }
            reactionsBuilder = reactionsBuilder.RecentReactions(recent);
        }

        using var messageReactions = reactionsBuilder.Build();
        return messageReactions.ToReadOnlySpan().ToArray();
    }

    public static TLMessageReactionInfo BuildReactionRow(int boxType, long boxId,
        int messageId, long reactorId, int peerType, long peerId, bool big, bool unread,
        int date, long order, List<byte[]> reactions)
    {
        var vector = new Vector();
        foreach (byte[] reaction in reactions)
        {
            vector.AppendTLObject(reaction);
        }

        var builder = MessageReactionInfo.Builder()
            .BoxType(boxType)
            .BoxId(boxId)
            .MessageId(messageId)
            .UserId(reactorId)
            .PeerType(peerType)
            .PeerId(peerId)
            .Date(date)
            .Order(order)
            .Reactions(vector);
        if (big)
        {
            builder = builder.Big(true);
        }
        if (unread)
        {
            builder = builder.Unread(true);
        }

        return builder.Build();
    }

    public async Task<(byte[]? DefaultReaction, List<byte[]> Recent)>
        ReadReactionSettingsAsync(long userId)
    {
        using var settings = await _messageReactionsRepository
            .GetReactionSettingsAsync(userId);
        if (settings == null)
        {
            return (null, new List<byte[]>());
        }

        var info = settings.Value.AsReactionSettingsInfo();
        byte[]? defaultReaction = info.Flags[0] ? info.DefaultReaction.ToArray() : null;
        var recent = new List<byte[]>();
        var vector = info.RecentReactions;
        int count = vector.Count;
        for (int i = 0; i < count; i++)
        {
            recent.Add(vector.ReadTLObject().ToArray());
        }

        return (defaultReaction, recent);
    }

    public void PutReactionSettings(long userId, byte[]? defaultReaction,
        List<byte[]> recent)
    {
        var vector = new Vector();
        foreach (byte[] reaction in recent)
        {
            vector.AppendTLObject(reaction);
        }
        var builder = ReactionSettingsInfo.Builder()
            .UserId(userId)
            .RecentReactions(vector);
        if (defaultReaction != null)
        {
            builder = builder.DefaultReaction(defaultReaction);
        }
        using TLReactionSettingsInfo settings = builder.Build();
        _messageReactionsRepository.PutReactionSettings(settings);
    }

    public static TLReactions BuildReactionsResultValue(List<byte[]> reactions,
        long clientHash)
    {
        long serverHash = ComputeReactionsHash(reactions);
        if (clientHash != 0 && clientHash == serverHash)
        {
            return ReactionsNotModified.Builder().Build();
        }

        var vector = new Vector();
        foreach (byte[] reaction in reactions)
        {
            vector.AppendTLObject(reaction);
        }

        return Reactions.Builder()
            .Hash(serverHash)
            .ReactionsProperty(vector)
            .Build();
    }

    public async Task<List<int>> GatherUnreadReactionMessageIds(int boxType,
        long boxId, long userId, bool requireChannelAuthor, TLPeer.PeerType peerType,
        long peerId)
    {
        var rows = await _messageReactionsRepository
            .GetBoxReactionsAsync(boxType, boxId);
        var messageIds = new HashSet<int>();
        foreach (var row in rows)
        {
            using var reactionRow = row;
            var info = reactionRow.AsMessageReactionInfo();
            if (!info.Unread)
            {
                continue;
            }
            if (boxType == MessageReactionBox.Common &&
                (info.PeerType != (int)peerType || info.PeerId != peerId))
            {
                continue;
            }
            messageIds.Add(info.MessageId);
        }

        if (!requireChannelAuthor)
        {
            return messageIds.ToList();
        }

        var authored = new List<int>();
        foreach (int id in messageIds)
        {
            var saved = await _channelMessagesRepository
                .GetMessageAsync(boxId, id);
            if (saved == null)
            {
                continue;
            }
            using var savedMessage = saved.Value;
            var original = savedMessage.AsSavedMessage().Get_OriginalMessage();
            if (original.Type == TLMessage.MessageType.Message &&
                ResolveChannelPostAuthorId(original) == userId)
            {
                authored.Add(id);
            }
        }

        return authored;
    }

    public async Task<int> ClearUnreadReactionRows(int boxType, long boxId,
        long userId, bool requireChannelAuthor, bool rebuildStoredCopies,
        int filterPeerType = 0, long filterPeerId = 0)
    {
        var affectedIds = await GatherUnreadReactionMessageIds(boxType, boxId, userId,
            requireChannelAuthor,
            filterPeerType == 0 ? TLPeer.PeerType.PeerUser : (TLPeer.PeerType)filterPeerType,
            filterPeerId);

        int cleared = 0;
        foreach (int messageId in affectedIds)
        {
            var rows = await _messageReactionsRepository
                .GetReactionsAsync(boxType, boxId, messageId);
            var entries = new List<ReactionEntry>();
            foreach (var row in rows)
            {
                using var reactionRow = row;
                var info = reactionRow.AsMessageReactionInfo();
                var reactions = new List<byte[]>();
                var vector = info.Reactions;
                int count = vector.Count;
                for (int i = 0; i < count; i++)
                {
                    reactions.Add(vector.ReadTLObject().ToArray());
                }
                if (info.Unread)
                {
                    using TLMessageReactionInfo updatedRow = BuildReactionRow(boxType,
                        boxId, messageId, info.UserId, info.PeerType, info.PeerId,
                        info.Big, unread: false, info.Date, info.Order, reactions);
                    _messageReactionsRepository.PutReaction(updatedRow);
                    cleared++;
                }
                entries.Add(new ReactionEntry(info.UserId, info.Big, false, info.Date,
                    info.Order, reactions));
            }

            if (!rebuildStoredCopies)
            {
                continue;
            }

            var saved = await _messageRepository.GetMessageAsync(boxId, messageId);
            if (saved == null)
            {
                continue;
            }
            using var savedMessage = saved.Value;
            var savedBody = savedMessage.AsSavedMessage();
            var original = savedBody.Get_OriginalMessage();
            if (original.Type != TLMessage.MessageType.Message)
            {
                continue;
            }
            var message = original.AsMessage();
            bool isGroup = filterPeerType == (int)TLPeer.PeerType.PeerChat;
            byte[] viewBytes = BuildMessageReactionsValue(entries, userId,
                includeRecent: true, canSeeList: isGroup, includeUnread: true);
            using TLMessage updated = message.Clone()
                .Reactions(viewBytes)
                .Build();
            _messageRepository.PutMessage(boxId, updated, savedBody.Pts);
        }

        return cleared;
    }

    private static long ComputeReactionsHash(List<byte[]> reactions)
    {
        var values = new List<long>();
        foreach (byte[] reactionBytes in reactions)
        {
            var emoji = (ReactionEmoji)reactionBytes.AsSpan();
            if (emoji.Constructor != Constructors.baseLayer_ReactionEmoji)
            {
                continue;
            }
            byte[] cleaned = RemoveEmojiSelectors(emoji.Emoticon);
            byte[] digest = System.Security.Cryptography.MD5.HashData(cleaned);
            values.Add(0);
            values.Add((digest[0] << 24) + (digest[1] << 16) + (digest[2] << 8) +
                       digest[3]);
        }

        return TelegramListHash.Compute(values);
    }

    private static byte[] RemoveEmojiSelectors(ReadOnlySpan<byte> emoji)
    {
        var result = new List<byte>(emoji.Length);
        for (int i = 0; i < emoji.Length; i++)
        {
            if (i + 3 <= emoji.Length && emoji[i] == 0xEF && emoji[i + 1] == 0xB8 &&
                emoji[i + 2] == 0x8F)
            {
                i += 2;
                continue;
            }
            result.Add(emoji[i]);
        }

        return result.ToArray();
    }
}
