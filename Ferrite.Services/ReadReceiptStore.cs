// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services;

/// <summary>
/// Writes and reads the dated per-reader receipts behind outbox read dates and
/// group read participants. A receipt is recorded once, when a message first
/// becomes read, so a later query reports when the recipient actually read it
/// rather than when it was asked.
/// </summary>
public sealed class ReadReceiptStore
{
    private readonly IChannelMessagesRepository _channelMessagesRepository;

    private readonly IMessageReactionsRepository _messageReactionsRepository;
    private readonly IMessageReadReceiptsRepository _messageReadReceiptsRepository;
    private readonly IMessageRepository _messageRepository;

    /// <summary>
    /// Pinned TDLib's default `pm_read_date_expire_period` and
    /// `chat_read_mark_expire_period` (`OptionManager.cpp:142`). Ferrite does not
    /// override either through appConfig, so the client and the server apply the
    /// same window.
    /// </summary>
    public const int ExpirePeriod = 604800;

    private readonly IUnitOfWork _unitOfWork;

    public ReadReceiptStore(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IMessageReactionsRepository messageReactionsRepository, IMessageReadReceiptsRepository messageReadReceiptsRepository, IMessageRepository messageRepository)
    {
        _channelMessagesRepository = channelMessagesRepository;

        _messageReactionsRepository = messageReactionsRepository;
        _messageReadReceiptsRepository = messageReadReceiptsRepository;
        _messageRepository = messageRepository;

        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Records a receipt for every incoming message in the reader's own box that
    /// this read advanced past. The caller owns the unit-of-work commit.
    /// </summary>
    public async Task<int> RecordCommonReceiptsAsync(long readerUserId,
        TLPeer.PeerType peerType, long peerId, int previousMaxId, int maxId, int date)
    {
        int windowEnd = maxId > 0 ? maxId : int.MaxValue;
        if (windowEnd <= previousMaxId)
        {
            return 0;
        }

        var newlyRead = new List<int>();
        IReadOnlyCollection<TLSavedMessage> saved = await _messageRepository
            .GetMessagesAsync(readerUserId);
        foreach (TLSavedMessage row in saved)
        {
            using TLSavedMessage savedMessage = row;
            TLMessage message = savedMessage.AsSavedMessage().Get_OriginalMessage();
            if (message.Type != TLMessage.MessageType.Message)
            {
                continue;
            }
            var body = message.AsMessage();
            if (body.OutProperty || body.Id <= previousMaxId || body.Id > windowEnd)
            {
                continue;
            }
            if (!MessageStore.TryReadStoredMessageInfo(message, out StoredMessageInfo info) ||
                info.PeerType != peerType || info.PeerId != peerId)
            {
                continue;
            }
            newlyRead.Add(body.Id);
        }

        int written = 0;
        foreach (int messageId in newlyRead)
        {
            using TLMessageCopyInfo? copy = await _messageReactionsRepository
                .GetCopyByOwnerMessageAsync(readerUserId, messageId);
            if (copy == null)
            {
                continue;
            }
            MessageIdentity identity = MessageIdentity.ForLogical(
                copy.Value.AsMessageCopyInfo().LogicalId);
            if (await TryPutReceiptAsync(identity, readerUserId, date))
            {
                written++;
            }
        }
        return written;
    }

    /// <summary>
    /// Records a receipt for every channel post other than the reader's own that
    /// this read advanced past. The caller owns the unit-of-work commit.
    /// </summary>
    public async Task<int> RecordChannelReceiptsAsync(long readerUserId, long channelId,
        int previousMaxId, int maxId, int date)
    {
        int windowEnd = maxId > 0 ? maxId : int.MaxValue;
        if (windowEnd <= previousMaxId)
        {
            return 0;
        }

        var newlyRead = new List<int>();
        // The shared channel box is queried by id range, so a read only walks the
        // posts it actually advanced past.
        IReadOnlyCollection<TLSavedMessage> saved = await _channelMessagesRepository.GetMessagesAsync(channelId, previousMaxId + 1,
                maxId > 0 ? maxId : 0);
        foreach (TLSavedMessage row in saved)
        {
            using TLSavedMessage savedMessage = row;
            TLMessage message = savedMessage.AsSavedMessage().Get_OriginalMessage();
            if (message.Type != TLMessage.MessageType.Message)
            {
                continue;
            }
            var body = message.AsMessage();
            if (body.Id <= previousMaxId || body.Id > windowEnd)
            {
                continue;
            }
            if (body.Flags[8] &&
                PeerResolver.TryReadPeer(body.Get_FromIdView(), out var from) &&
                from.Type == TLPeer.PeerType.PeerUser && from.Id == readerUserId)
            {
                continue;
            }
            newlyRead.Add(body.Id);
        }

        int written = 0;
        foreach (int messageId in newlyRead)
        {
            if (await TryPutReceiptAsync(
                    MessageIdentity.ForChannel(channelId, messageId), readerUserId,
                    date))
            {
                written++;
            }
        }
        return written;
    }

    /// <summary>
    /// The date the given reader first read the message, or null when it is still
    /// unread or the receipt has aged past <see cref="ExpirePeriod"/>.
    /// </summary>
    public async ValueTask<int?> GetReadDateAsync(MessageIdentity identity,
        long readerUserId, int now)
    {
        using TLMessageReadReceipt? receipt = await _messageReadReceiptsRepository.GetReadReceiptAsync(identity, readerUserId);
        if (receipt == null)
        {
            return null;
        }
        int date = receipt.Value.AsMessageReadReceipt().Date;
        return now - date > ExpirePeriod ? null : date;
    }

    private async ValueTask<bool> TryPutReceiptAsync(MessageIdentity identity,
        long readerUserId, int date)
    {
        using (TLMessageReadReceipt? existing = await _messageReadReceiptsRepository.GetReadReceiptAsync(identity,
                       readerUserId))
        {
            if (existing != null)
            {
                return false;
            }
        }

        using TLMessageReadReceipt receipt = MessageReadReceipt.Builder()
            .BoxType(identity.BoxType)
            .BoxId(identity.BoxId)
            .MessageId(identity.MessageId)
            .UserId(readerUserId)
            .Date(date)
            .Build();
        _messageReadReceiptsRepository.PutReadReceipt(receipt);
        return true;
    }
}
