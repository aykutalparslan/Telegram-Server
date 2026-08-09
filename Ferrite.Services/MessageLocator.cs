// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services;

public readonly record struct StoredMessageLocation(
    int BoxType,
    long BoxId,
    long OwnerId,
    int MessageId,
    int Pts,
    long? LogicalId,
    byte[] MessageBytes);

/// <summary>
/// Resolves a caller-local common-box id to all copies of the same logical
/// message, or resolves a shared channel message. Mutations retain each copy's
/// local id and pts and may refresh its search row.
/// </summary>
public sealed class MessageLocator
{
    private readonly IChannelMessagesRepository _channelMessagesRepository;
    private readonly IMessageReactionsRepository _messageReactionsRepository;
    private readonly IMessageRepository _messageRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ISearchEngine _search;

    public MessageLocator(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IMessageReactionsRepository messageReactionsRepository, IMessageRepository messageRepository, ISearchEngine search)
    {
        _channelMessagesRepository = channelMessagesRepository;
        _messageReactionsRepository = messageReactionsRepository;
        _messageRepository = messageRepository;

        _unitOfWork = unitOfWork;
        _search = search;
    }

    public async ValueTask<StoredMessageLocation?> FindCommonAsync(long ownerId,
        int messageId)
    {
        using TLSavedMessage? saved = await _messageRepository
            .GetMessageAsync(ownerId, messageId);
        if (saved == null)
        {
            return null;
        }

        var body = saved.Value.AsSavedMessage();
        byte[] messageBytes = body.Get_OriginalMessage().AsSpan().ToArray();
        int pts = body.Pts;
        long? logicalId = null;
        using (TLMessageCopyInfo? copy = await _messageReactionsRepository
                   .GetCopyByOwnerMessageAsync(ownerId, messageId))
        {
            if (copy != null)
            {
                logicalId = copy.Value.AsMessageCopyInfo().LogicalId;
            }
        }

        return new StoredMessageLocation(MessageReactionBox.Common, ownerId,
            ownerId, messageId, pts, logicalId, messageBytes);
    }

    public async ValueTask<IReadOnlyList<StoredMessageLocation>> FindCommonCopiesAsync(
        long ownerId, int messageId)
    {
        StoredMessageLocation? origin = await FindCommonAsync(ownerId, messageId);
        if (origin == null)
        {
            return Array.Empty<StoredMessageLocation>();
        }
        if (origin.Value.LogicalId == null)
        {
            return new[] { origin.Value };
        }

        IReadOnlyCollection<TLMessageCopyInfo> mappings = await _messageReactionsRepository.GetMessageCopiesAsync(origin.Value.LogicalId.Value);
        var locations = new List<StoredMessageLocation>();
        var seen = new HashSet<(long OwnerId, int MessageId)>();
        foreach (TLMessageCopyInfo mapping in mappings)
        {
            long copyOwnerId;
            int copyMessageId;
            using (mapping)
            {
                var info = mapping.AsMessageCopyInfo();
                copyOwnerId = info.UserId;
                copyMessageId = info.MessageId;
            }
            if (!seen.Add((copyOwnerId, copyMessageId)))
            {
                continue;
            }

            using TLSavedMessage? saved = await _messageRepository
                .GetMessageAsync(copyOwnerId, copyMessageId);
            if (saved == null)
            {
                continue;
            }
            var body = saved.Value.AsSavedMessage();
            locations.Add(new StoredMessageLocation(MessageReactionBox.Common,
                copyOwnerId, copyOwnerId, copyMessageId, body.Pts,
                origin.Value.LogicalId,
                body.Get_OriginalMessage().AsSpan().ToArray()));
        }

        if (seen.Add((ownerId, messageId)))
        {
            locations.Add(origin.Value);
        }
        return locations
            .OrderByDescending(x => x.OwnerId == ownerId && x.MessageId == messageId)
            .ThenBy(x => x.OwnerId)
            .ThenBy(x => x.MessageId)
            .ToArray();
    }

    /// <summary>
    /// Resolves the identity that per-message interaction and receipt state is
    /// keyed by: the shared channel post, or the logical message behind the
    /// caller's own common-box copy. The requested id must actually live in the
    /// addressed dialog, so a caller cannot reach another conversation's state by
    /// naming an unrelated peer.
    /// </summary>
    public async ValueTask<MessageIdentity?> ResolveIdentityAsync(long callerUserId,
        TLPeer.PeerType peerType, long peerId, int messageId)
    {
        if (messageId <= 0 || peerId <= 0)
        {
            return null;
        }

        if (peerType == TLPeer.PeerType.PeerChannel)
        {
            StoredMessageLocation? channelLocation =
                await FindChannelAsync(peerId, messageId);
            return channelLocation == null
                ? null
                : MessageIdentity.ForChannel(peerId, messageId);
        }

        StoredMessageLocation? location = await FindCommonAsync(callerUserId, messageId);
        if (location is not { LogicalId: not null })
        {
            return null;
        }

        byte[] bytes = location.Value.MessageBytes;
        using var stored = new TLMessage(bytes, 0, bytes.Length);
        if (!MessageStore.TryReadStoredMessageInfo(stored, out StoredMessageInfo info) ||
            info.PeerType != peerType || info.PeerId != peerId)
        {
            return null;
        }
        return MessageIdentity.ForLogical(location.Value.LogicalId.Value);
    }

    public async ValueTask<StoredMessageLocation?> FindChannelAsync(long channelId,
        int messageId)
    {
        using TLSavedMessage? saved = await _channelMessagesRepository
            .GetMessageAsync(channelId, messageId);
        if (saved == null)
        {
            return null;
        }
        var body = saved.Value.AsSavedMessage();
        return new StoredMessageLocation(MessageReactionBox.Channel, channelId,
            0, messageId, body.Pts, null,
            body.Get_OriginalMessage().AsSpan().ToArray());
    }

    public async Task<IReadOnlyList<StoredMessageLocation>> MutateCommonCopiesAsync(
        long ownerId, int messageId,
        Func<StoredMessageLocation, byte[]> mutation,
        bool refreshSearch = false)
    {
        IReadOnlyList<StoredMessageLocation> locations =
            await FindCommonCopiesAsync(ownerId, messageId);
        var updated = new List<StoredMessageLocation>(locations.Count);
        foreach (StoredMessageLocation location in locations)
        {
            byte[] bytes = mutation(location);
            using var message = new TLMessage(bytes, 0, bytes.Length);
            if (!HasId(message, location.MessageId))
            {
                throw new InvalidOperationException(
                    "A common-box mutation must preserve the copy-local message id.");
            }
            _messageRepository.PutMessage(location.OwnerId, message,
                location.Pts);
            var value = location with { MessageBytes = bytes };
            updated.Add(value);
            if (refreshSearch)
            {
                await RefreshSearchAsync(value, message);
            }
        }
        return updated;
    }

    public async Task<StoredMessageLocation?> MutateChannelAsync(long channelId,
        int messageId, Func<StoredMessageLocation, byte[]> mutation,
        bool refreshSearch = false)
    {
        StoredMessageLocation? location = await FindChannelAsync(channelId, messageId);
        if (location == null)
        {
            return null;
        }

        byte[] bytes = mutation(location.Value);
        using var message = new TLMessage(bytes, 0, bytes.Length);
        if (!HasId(message, messageId))
        {
            throw new InvalidOperationException(
                "A channel mutation must preserve the message id.");
        }
        _channelMessagesRepository.PutMessage(channelId, message,
            location.Value.Pts);
        StoredMessageLocation updated = location.Value with { MessageBytes = bytes };
        if (refreshSearch)
        {
            await RefreshSearchAsync(updated, message);
        }
        return updated;
    }

    private async ValueTask RefreshSearchAsync(StoredMessageLocation location,
        TLMessage message)
    {
        string key = location.BoxId + "_" + location.MessageId;
        if (message.Type != TLMessage.MessageType.Message)
        {
            await _search.DeleteMessage(key);
            return;
        }

        var body = message.AsMessage();
        var peer = PeerResolver.TryReadPeer(body.Get_PeerIdView(), out var resolvedPeer)
            ? resolvedPeer
            : default;
        var from = body.Flags[8] &&
                   PeerResolver.TryReadPeer(body.Get_FromIdView(), out var resolvedFrom)
            ? resolvedFrom
            : (Type: TLPeer.PeerType.PeerUser, Id: location.OwnerId);
        var model = new MessageSearchModel(key, location.BoxId,
            (int)from.Type, from.Id, (int)peer.Type, peer.Id, body.Id, null,
            Encoding.UTF8.GetString(body.MessageProperty), body.Date);
        await _search.IndexMessage(model);
    }

    private static bool HasId(TLMessage message, int id) => message.Type switch
    {
        TLMessage.MessageType.Message => message.AsMessage().Id == id,
        TLMessage.MessageType.MessageService => message.AsMessageService().Id == id,
        TLMessage.MessageType.MessageEmpty => message.AsMessageEmpty().Id == id,
        _ => false,
    };
}
