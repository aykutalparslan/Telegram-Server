// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

/// <summary>
/// Resolves edit access from the stored message row. The result's caption flag
/// describes the kind of content that can be caption-edited; it does not mean
/// that the current caption is non-empty.
/// </summary>
public sealed class GetMessageEditDataHandler
{
    private readonly IChatParticipantsRepository _chatParticipantsRepository;

    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChannelMessagesRepository _channelMessagesRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IUserRepository _userRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public GetMessageEditDataHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IAuthorizationRepository authorizationRepository, IChannelMessagesRepository channelMessagesRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,
        TimeProvider timeProvider)
    {
        _chatParticipantsRepository = chatParticipantsRepository;

        _authorizationRepository = authorizationRepository;
        _channelMessagesRepository = channelMessagesRepository;
        _chatRepository = chatRepository;
        _messageRepository = messageRepository;
        _userRepository = userRepository;

        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    [TLFunction(Constructors.baseLayer_GetMessageEditData)]
    public async ValueTask<TLMessageEditData> Handle(long authKeyId, TLBytes q)
    {
        long userId;
        using (TLAuthInfo? auth = await _authorizationRepository
                   .GetAuthorizationAsync(authKeyId))
        {
            if (auth == null || !auth.Value.AsAuthInfo().LoggedIn)
            {
                return Error(400, "AUTH_KEY_INVALID");
            }
            userId = auth.Value.AsAuthInfo().UserId;
        }

        var request = (GetMessageEditData)q;
        if (!PeerResolver.TryResolveInputPeerDialogKey(request.Get_PeerView(),
                userId, out DialogPeerKey peer))
        {
            return Error(400, "PEER_ID_INVALID");
        }
        int messageId = request.Id;
        if (messageId <= 0)
        {
            return Error(400, "MESSAGE_ID_INVALID");
        }

        return peer.Type == TLPeer.PeerType.PeerChannel
            ? await GetChannelEditDataAsync(userId, peer.Id, messageId)
            : await GetCommonBoxEditDataAsync(userId, peer, messageId);
    }

    private async ValueTask<TLMessageEditData> GetCommonBoxEditDataAsync(
        long userId, DialogPeerKey peer, int messageId)
    {
        string? accessError = await ValidateCommonPeerAsync(userId, peer);
        if (accessError != null)
        {
            return Error(accessError == "CHAT_WRITE_FORBIDDEN" ? 403 : 400,
                accessError);
        }

        using TLSavedMessage? saved = await _messageRepository
            .GetMessageAsync(userId, messageId);
        if (saved == null)
        {
            return Error(400, "MESSAGE_ID_INVALID");
        }

        return EvaluateStoredMessage(saved.Value, userId, peer,
            allowAdministrativeEdit: false,
            exemptFromTimeLimit: peer.Type == TLPeer.PeerType.PeerUser &&
                                 peer.Id == userId);
    }

    private async ValueTask<TLMessageEditData> GetChannelEditDataAsync(
        long userId, long channelId, int messageId)
    {
        if (channelId <= 0)
        {
            return Error(400, "PEER_ID_INVALID");
        }

        bool broadcast;
        byte[] channelBytes;
        using (TLChat? chat = await _chatRepository
                   .GetChatAsync(channelId))
        {
            if (chat == null || chat.Value.Type != TLChat.ChatType.Channel)
            {
                return Error(400, "PEER_ID_INVALID");
            }
            broadcast = chat.Value.AsChannel().Broadcast;
            channelBytes = chat.Value.AsSpan().ToArray();
        }

        bool canAdministrativelyEdit;
        using (TLChatParticipantInfo? participant = await _chatParticipantsRepository.GetParticipantAsync(channelId,
                       userId))
        {
            if (participant == null || !IsActive(participant.Value))
            {
                return Error(403, "CHAT_WRITE_FORBIDDEN");
            }

            canAdministrativelyEdit = broadcast &&
                                      MessageEditRules.HasEditMessagesRight(
                                          participant.Value);
            bool isAdmin = ChatRights.HasAdminRight(participant.Value,
                ChatAdminRightRequirement.Any);
            int now = UnixNow();
            if (!isAdmin &&
                (ChatRights.IsRestrictedFrom(participant.Value,
                     ChatBannedAction.SendMessages, now) ||
                 ChatRights.DefaultBans(channelBytes,
                     ChatBannedAction.SendMessages)))
            {
                return Error(403, "CHAT_WRITE_FORBIDDEN");
            }
        }

        using TLSavedMessage? saved = await _channelMessagesRepository
            .GetMessageAsync(channelId, messageId);
        if (saved == null)
        {
            return Error(400, "MESSAGE_ID_INVALID");
        }

        var peer = new DialogPeerKey(TLPeer.PeerType.PeerChannel, channelId);
        TLMessageEditData result = EvaluateStoredMessage(saved.Value, userId,
            peer, canAdministrativelyEdit,
            exemptFromTimeLimit: canAdministrativelyEdit);
        if (result.Type == TLMessageEditData.MessageEditDataType.RpcError &&
            broadcast && !canAdministrativelyEdit)
        {
            var error = new Ferrite.TL.mtproto.RpcError(result.AsSpan());
            if (System.Text.Encoding.UTF8.GetString(error.ErrorMessage) ==
                "MESSAGE_AUTHOR_REQUIRED")
            {
                result.Dispose();
                return Error(400, "CHAT_ADMIN_REQUIRED");
            }
        }
        return result;
    }

    private TLMessageEditData EvaluateStoredMessage(TLSavedMessage saved,
        long userId, DialogPeerKey requestedPeer, bool allowAdministrativeEdit,
        bool exemptFromTimeLimit)
    {
        TLMessage original = saved.AsSavedMessage().Get_OriginalMessage();
        if (original.Type != TLMessage.MessageType.Message ||
            !MessageStore.TryReadStoredMessageInfo(original, out var info) ||
            info.PeerType != requestedPeer.Type || info.PeerId != requestedPeer.Id)
        {
            return Error(400, "MESSAGE_ID_INVALID");
        }

        var message = original.AsMessage();
        if (!MessageEditRules.IsAuthoredBy(message, userId) &&
            !allowAdministrativeEdit)
        {
            return Error(403, "MESSAGE_AUTHOR_REQUIRED");
        }

        if (MessageEditRules.IsExpired(message, UnixNow(), exemptFromTimeLimit))
        {
            return Error(400, "MESSAGE_EDIT_TIME_EXPIRED");
        }

        return MessageEditData.Builder()
            .Caption(IsCaptionEditable(message))
            .Build();
    }

    private async ValueTask<string?> ValidateCommonPeerAsync(long userId,
        DialogPeerKey peer)
    {
        if (peer.Id <= 0)
        {
            return "PEER_ID_INVALID";
        }
        if (peer.Type == TLPeer.PeerType.PeerUser)
        {
            using TLUser? user = _userRepository.GetUser(peer.Id);
            return user == null ? "PEER_ID_INVALID" : null;
        }
        if (peer.Type != TLPeer.PeerType.PeerChat)
        {
            return "PEER_ID_INVALID";
        }

        byte[] chatBytes;
        using (TLChat? chat = await _chatRepository.GetChatAsync(peer.Id))
        {
            if (chat == null || chat.Value.Type != TLChat.ChatType.Chat ||
                chat.Value.AsChat().Deactivated)
            {
                return "PEER_ID_INVALID";
            }
            chatBytes = chat.Value.AsSpan().ToArray();
        }

        using TLChatParticipantInfo? participant = await _chatParticipantsRepository.GetParticipantAsync(peer.Id, userId);
        if (participant == null || !IsActive(participant.Value))
        {
            return "CHAT_WRITE_FORBIDDEN";
        }
        bool isAdmin = ChatRights.HasAdminRight(participant.Value,
            ChatAdminRightRequirement.Any);
        if (!isAdmin &&
            (ChatRights.IsRestrictedFrom(participant.Value,
                 ChatBannedAction.SendMessages, UnixNow()) ||
             ChatRights.DefaultBans(chatBytes, ChatBannedAction.SendMessages)))
        {
            return "CHAT_WRITE_FORBIDDEN";
        }
        return null;
    }

    private static bool IsCaptionEditable(Message message)
    {
        if (!message.Flags[9])
        {
            return false;
        }
        MessageMediaView media = message.Get_MediaView();
        return media.Is(out MessageMediaPhoto _) ||
               media.Is(out MessageMediaDocument _) ||
               media.Is(out MessageMediaPaidMedia _);
    }

    private static bool IsActive(TLChatParticipantInfo participant)
    {
        int role = participant.AsChatParticipantInfo().Role;
        return role != (int)ChatParticipantRole.Banned &&
               role != (int)ChatParticipantRole.Left;
    }

    private int UnixNow() => checked((int)_timeProvider.GetUtcNow()
        .ToUnixTimeSeconds());

    private static TLMessageEditData Error(int code, string message) =>
        (TLMessageEditData)RpcErrorGenerator.GenerateError(code,
            System.Text.Encoding.UTF8.GetBytes(message));
}
