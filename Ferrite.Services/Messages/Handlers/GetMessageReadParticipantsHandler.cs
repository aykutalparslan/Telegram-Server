// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class GetMessageReadParticipantsHandler
{
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IMessageReadReceiptsRepository _messageReadReceiptsRepository;

    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatRepository _chatRepository;

    private const int MaxParticipantCount = 100;

    private readonly IUnitOfWork _unitOfWork;
    private readonly MessageLocator _locator;
    private readonly TimeProvider _timeProvider;

    public GetMessageReadParticipantsHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IMessageReadReceiptsRepository messageReadReceiptsRepository, IAuthorizationRepository authorizationRepository, IChatRepository chatRepository,
        MessageLocator locator, TimeProvider timeProvider)
    {
        _chatParticipantsRepository = chatParticipantsRepository;
        _messageReadReceiptsRepository = messageReadReceiptsRepository;

        _authorizationRepository = authorizationRepository;
        _chatRepository = chatRepository;

        _unitOfWork = unitOfWork;
        _locator = locator;
        _timeProvider = timeProvider;
    }

    [TLFunction(Constructors.baseLayer_GetMessageReadParticipants)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long userId;
        using (TLAuthInfo? auth = await _authorizationRepository
                   .GetAuthorizationAsync(authKeyId))
        {
            if (auth == null || !auth.Value.AsAuthInfo().LoggedIn)
            {
                return Error("AUTH_KEY_INVALID");
            }
            userId = auth.Value.AsAuthInfo().UserId;
        }

        var request = (GetMessageReadParticipants)q;
        if (!PeerResolver.TryResolveInputPeerDialogKey(request.Get_PeerView(),
                userId, out DialogPeerKey peer) ||
            peer.Type is not (TLPeer.PeerType.PeerChat or
                TLPeer.PeerType.PeerChannel))
        {
            return Error("PEER_ID_INVALID");
        }
        int messageId = request.MsgId;

        string? accessError = await ValidateAccessAsync(userId, peer);
        if (accessError != null)
        {
            return Error(accessError);
        }

        MessageIdentity? identity = await _locator.ResolveIdentityAsync(userId,
            peer.Type, peer.Id, messageId);
        if (identity == null)
        {
            return Error("MESSAGE_ID_INVALID");
        }

        StoredMessageLocation? location = peer.Type == TLPeer.PeerType.PeerChannel
            ? await _locator.FindChannelAsync(peer.Id, messageId)
            : await _locator.FindCommonAsync(userId, messageId);
        if (location == null)
        {
            return Error("MESSAGE_ID_INVALID");
        }

        int messageDate;
        bool anonymousPoll;
        {
            byte[] bytes = location.Value.MessageBytes;
            using var stored = new TLMessage(bytes, 0, bytes.Length);
            if (stored.Type != TLMessage.MessageType.Message)
            {
                return Error("MESSAGE_ID_INVALID");
            }

            var message = stored.AsMessage();
            if ((peer.Type == TLPeer.PeerType.PeerChat && !message.OutProperty) ||
                !message.Flags[8] ||
                !PeerResolver.TryReadPeer(message.Get_FromIdView(), out var author) ||
                author.Type != TLPeer.PeerType.PeerUser || author.Id != userId ||
                !PeerResolver.TryReadPeer(message.Get_PeerIdView(), out var dialog) ||
                dialog.Type != peer.Type || dialog.Id != peer.Id)
            {
                return Error("MESSAGE_ID_INVALID");
            }
            messageDate = message.Date;
            anonymousPoll = IsAnonymousPoll(message);
        }

        int now = checked((int)_timeProvider.GetUtcNow().ToUnixTimeSeconds());
        if (messageDate <= 0 || messageDate > now)
        {
            return Error("MESSAGE_ID_INVALID");
        }
        if (now - messageDate > ReadReceiptStore.ExpirePeriod)
        {
            return Error("MESSAGE_TOO_OLD");
        }
        if (anonymousPoll)
        {
            return Error("POLL_VOTERS_FORBIDDEN");
        }

        IReadOnlyCollection<TLMessageReadReceipt> rows = await _messageReadReceiptsRepository.GetReadReceiptsAsync(identity.Value);
        List<(long UserId, int Date)> readers = ReadReaders(rows, identity.Value,
            userId, messageDate, now);
        return BuildResult(readers);
    }

    private async ValueTask<string?> ValidateAccessAsync(long userId,
        DialogPeerKey peer)
    {
        if (peer.Id <= 0)
        {
            return "PEER_ID_INVALID";
        }

        using (TLChat? stored = await _chatRepository
                   .GetChatAsync(peer.Id))
        {
            if (stored == null)
            {
                return peer.Type == TLPeer.PeerType.PeerChannel
                    ? "CHANNEL_INVALID"
                    : "CHAT_ID_INVALID";
            }

            if (peer.Type == TLPeer.PeerType.PeerChat)
            {
                if (stored.Value.Type != TLChat.ChatType.Chat)
                {
                    return "CHAT_ID_INVALID";
                }
                var chat = stored.Value.AsChat();
                if (chat.Deactivated || chat.Left)
                {
                    return "CHAT_ID_INVALID";
                }
            }
            else
            {
                if (stored.Value.Type != TLChat.ChatType.Channel)
                {
                    return "CHANNEL_INVALID";
                }
                var channel = stored.Value.AsChannel();
                if (channel.Broadcast || !channel.Megagroup)
                {
                    return "CHANNEL_INVALID";
                }
            }
        }

        IReadOnlyCollection<TLChatParticipantInfo> rows = await _chatParticipantsRepository.GetParticipantsAsync(peer.Id);
        var activeUserIds = new HashSet<long>();
        foreach (TLChatParticipantInfo row in rows)
        {
            using (row)
            {
                var participant = row.AsChatParticipantInfo();
                if (participant.ChatId == peer.Id && participant.UserId > 0 &&
                    IsActive(row))
                {
                    activeUserIds.Add(participant.UserId);
                }
            }
        }

        if (!activeUserIds.Contains(userId))
        {
            return peer.Type == TLPeer.PeerType.PeerChannel
                ? "CHANNEL_PRIVATE"
                : "USER_NOT_PARTICIPANT";
        }
        if (activeUserIds.Count is < 1 or > MaxParticipantCount)
        {
            return "CHAT_TOO_BIG";
        }
        return null;
    }

    private static bool IsAnonymousPoll(Message message)
    {
        if (!message.Flags[9] ||
            !message.Get_MediaView().Is(out MessageMediaPoll media))
        {
            return false;
        }
        var poll = (Poll)media.Poll;
        return !poll.PublicVoters;
    }

    private static List<(long UserId, int Date)> ReadReaders(
        IReadOnlyCollection<TLMessageReadReceipt> rows, MessageIdentity identity,
        long authorUserId, int messageDate, int now)
    {
        var firstReadDates = new Dictionary<long, int>();
        foreach (TLMessageReadReceipt row in rows)
        {
            using (row)
            {
                var receipt = row.AsMessageReadReceipt();
                if (receipt.BoxType != identity.BoxType ||
                    receipt.BoxId != identity.BoxId ||
                    receipt.MessageId != identity.MessageId ||
                    receipt.UserId <= 0 || receipt.UserId == authorUserId ||
                    receipt.Date < messageDate || receipt.Date > now)
                {
                    continue;
                }

                if (!firstReadDates.TryGetValue(receipt.UserId, out int firstDate) ||
                    receipt.Date < firstDate)
                {
                    firstReadDates[receipt.UserId] = receipt.Date;
                }
            }
        }

        return firstReadDates
            .Select(x => (UserId: x.Key, Date: x.Value))
            .OrderByDescending(x => x.Date)
            .ThenBy(x => x.UserId)
            .ToList();
    }

    private static TLBytes BuildResult(IReadOnlyList<(long UserId, int Date)> readers)
    {
        var result = new Vector();
        foreach ((long userId, int date) in readers)
        {
            using TLReadParticipantDate reader = ReadParticipantDate.Builder()
                .UserId(userId)
                .Date(date)
                .Build();
            result.AppendTLObject(reader.AsSpan());
        }
        byte[] bytes = result.ToReadOnlySpan().ToArray();
        return new TLBytes(bytes, 0, bytes.Length);
    }

    private static bool IsActive(TLChatParticipantInfo participant)
    {
        int role = participant.AsChatParticipantInfo().Role;
        return role != (int)ChatParticipantRole.Banned &&
               role != (int)ChatParticipantRole.Left;
    }

    private static TLBytes Error(string message) =>
        RpcErrorGenerator.GenerateError(400, Encoding.UTF8.GetBytes(message));
}
