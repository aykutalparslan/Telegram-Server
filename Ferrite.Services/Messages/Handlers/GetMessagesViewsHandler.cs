// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

// `messages.MessageViews` and the bare `MessageViews` element both generate a
// `TLMessageViews` union, one per namespace, so both are named explicitly here.
using TLMessagesMessageViews = Ferrite.TL.baseLayer.messages.TLMessageViews;
using TLMessageViewsElement = Ferrite.TL.baseLayer.TLMessageViews;

namespace Ferrite.Services.Handlers.MessageMethods;

/// <summary>
/// Serves the durable view/forward counters for the requested messages. The
/// pinned client rejects a result whose length differs from the request, so a
/// message the caller cannot resolve still occupies its slot as a
/// <c>messageViews</c> with no counters. Incrementing is idempotent per viewer:
/// the per-viewer receipt row is the index and only a first receipt advances the
/// served counter.
/// </summary>
public sealed class GetMessagesViewsHandler
{
    private readonly IChatParticipantsRepository _chatParticipantsRepository;

    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IMessageInteractionsRepository _messageInteractionsRepository;
    private readonly IUserRepository _userRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly MessageLocator _locator;
    private readonly UpdateFanout _fanout;
    private readonly TimeProvider _timeProvider;

    public GetMessagesViewsHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IAuthorizationRepository authorizationRepository, IChatRepository chatRepository, IMessageInteractionsRepository messageInteractionsRepository, IUserRepository userRepository, MessageLocator locator,
        UpdateFanout fanout, TimeProvider timeProvider)
    {
        _chatParticipantsRepository = chatParticipantsRepository;

        _authorizationRepository = authorizationRepository;
        _chatRepository = chatRepository;
        _messageInteractionsRepository = messageInteractionsRepository;
        _userRepository = userRepository;

        _unitOfWork = unitOfWork;
        _locator = locator;
        _fanout = fanout;
        _timeProvider = timeProvider;
    }

    [TLFunction(Constructors.baseLayer_GetMessagesViews)]
    public async Task<TLMessagesMessageViews> Handle(long authKeyId, TLBytes q)
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

        var request = (GetMessagesViews)q;
        if (!PeerResolver.TryResolveInputPeerDialogKey(request.Get_PeerView(),
                userId, out DialogPeerKey key))
        {
            return Error("PEER_ID_INVALID");
        }
        TLPeer.PeerType peerType = key.Type;
        long peerId = key.Id;
        List<int> requestedIds = ReadRequestedIds(request.Id);
        bool increment = request.Increment;

        string? accessError = await ValidateAccessAsync(userId, peerType, peerId);
        if (accessError != null)
        {
            return Error(accessError);
        }

        int date = checked((int)_timeProvider.GetUtcNow().ToUnixTimeSeconds());
        var counters = new List<(int Views, int Forwards)?>(requestedIds.Count);
        // The result must have one element per requested id, so duplicates keep
        // their slots. Their increments are collapsed here rather than relying on
        // an uncommitted receipt write being visible to the next read.
        var applied = new Dictionary<MessageIdentity, (int Views, int Forwards)>();
        bool mutated = false;
        foreach (int messageId in requestedIds)
        {
            MessageIdentity? identity = await _locator.ResolveIdentityAsync(userId,
                peerType, peerId, messageId);
            if (identity == null)
            {
                counters.Add(null);
                continue;
            }
            if (applied.TryGetValue(identity.Value,
                    out (int Views, int Forwards) seen))
            {
                counters.Add(seen);
                continue;
            }

            (int Views, int Forwards) current =
                await ReadCountersAsync(identity.Value);
            if (increment && await TryRecordViewAsync(identity.Value, userId, date))
            {
                current = current with { Views = current.Views + 1 };
                WriteCounters(identity.Value, current);
                mutated = true;
            }
            applied[identity.Value] = current;
            counters.Add(current);
        }

        if (mutated && !await _unitOfWork.SaveAsync())
        {
            return Error("INTERNAL_SERVER_ERROR");
        }

        List<byte[]> chatBytes = peerType == TLPeer.PeerType.PeerUser
            ? new List<byte[]>()
            : await _fanout.GetChatBytesForViewerAsync(userId, new[] { peerId });
        return BuildResult(counters, peerType, peerId, chatBytes);
    }

    private async ValueTask<(int Views, int Forwards)> ReadCountersAsync(
        MessageIdentity identity)
    {
        using TLMessageInteractionInfo? stored = await _messageInteractionsRepository.GetInteractionInfoAsync(identity);
        if (stored == null)
        {
            return (0, 0);
        }
        var info = stored.Value.AsMessageInteractionInfo();
        return (info.Views, info.Forwards);
    }

    private async ValueTask<bool> TryRecordViewAsync(MessageIdentity identity,
        long userId, int date)
    {
        using (TLMessageViewReceipt? existing = await _messageInteractionsRepository.GetViewReceiptAsync(identity, userId))
        {
            if (existing != null)
            {
                return false;
            }
        }

        using TLMessageViewReceipt receipt = MessageViewReceipt.Builder()
            .BoxType(identity.BoxType)
            .BoxId(identity.BoxId)
            .MessageId(identity.MessageId)
            .UserId(userId)
            .Date(date)
            .Build();
        _messageInteractionsRepository.PutViewReceipt(receipt);
        return true;
    }

    private void WriteCounters(MessageIdentity identity,
        (int Views, int Forwards) counters)
    {
        using TLMessageInteractionInfo info = MessageInteractionInfo.Builder()
            .BoxType(identity.BoxType)
            .BoxId(identity.BoxId)
            .MessageId(identity.MessageId)
            .Views(counters.Views)
            .Forwards(counters.Forwards)
            .Build();
        _messageInteractionsRepository.PutInteractionInfo(info);
    }

    private async ValueTask<string?> ValidateAccessAsync(long userId,
        TLPeer.PeerType peerType, long peerId)
    {
        if (peerId <= 0)
        {
            return "PEER_ID_INVALID";
        }
        if (peerType == TLPeer.PeerType.PeerUser)
        {
            using TLUser? user = _userRepository.GetUser(peerId);
            return user == null ? "PEER_ID_INVALID" : null;
        }

        bool isChannel;
        using (TLChat? chat = await _chatRepository.GetChatAsync(peerId))
        {
            if (chat == null)
            {
                return peerType == TLPeer.PeerType.PeerChannel
                    ? "CHANNEL_INVALID"
                    : "CHAT_ID_INVALID";
            }
            isChannel = chat.Value.Type == TLChat.ChatType.Channel;
        }
        if (isChannel != (peerType == TLPeer.PeerType.PeerChannel))
        {
            return "PEER_ID_INVALID";
        }

        using TLChatParticipantInfo? participant = await _chatParticipantsRepository.GetParticipantAsync(peerId, userId);
        if (participant == null || !IsActive(participant.Value))
        {
            return isChannel ? "CHANNEL_PRIVATE" : "USER_NOT_PARTICIPANT";
        }
        return null;
    }

    // Synchronous so the ref-struct vectors never cross an await.
    private TLMessagesMessageViews BuildResult(
        IReadOnlyList<(int Views, int Forwards)?> counters,
        TLPeer.PeerType peerType, long peerId, IReadOnlyList<byte[]> chatBytes)
    {
        var views = new Vector();
        foreach ((int Views, int Forwards)? counter in counters)
        {
            var builder = MessageViews.Builder();
            if (counter != null)
            {
                builder = builder
                    .Views(counter.Value.Views)
                    .Forwards(counter.Value.Forwards);
            }
            using TLMessageViewsElement element = builder.Build();
            views.AppendTLObject(element.AsSpan());
        }

        var chats = new Vector();
        foreach (byte[] bytes in chatBytes)
        {
            chats.AppendTLObject(bytes);
        }
        var users = new Vector();
        if (peerType == TLPeer.PeerType.PeerUser)
        {
            _fanout.AppendUsers(ref users, new[] { peerId });
        }

        return MessagesMessageViews.Builder()
            .Views(views)
            .Chats(chats)
            .Users(users)
            .Build();
    }

    private static List<int> ReadRequestedIds(VectorOfInt ids)
    {
        var requested = new List<int>(ids.Count);
        for (int i = 0; i < ids.Count; i++)
        {
            requested.Add(ids[i]);
        }
        return requested;
    }

    private static bool IsActive(TLChatParticipantInfo participant)
    {
        int role = participant.AsChatParticipantInfo().Role;
        return role != (int)ChatParticipantRole.Banned &&
               role != (int)ChatParticipantRole.Left;
    }

    private static TLMessagesMessageViews Error(string message) =>
        (TLMessagesMessageViews)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));
}
