// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services;

public sealed record PreparedMessageTarget(
    TLPeer.PeerType PeerType,
    long PeerId,
    DialogPeerKey Sender,
    IReadOnlyList<long> RelatedUserIds,
    byte[]? ChatBytes,
    bool Broadcast,
    int ForumTopicId,
    StoredMessageForumTopic? ForumTopic,
    string? Error);

// Shared Phase-6 peer/membership/rights/forum preflight used by text and media
// sends. It snapshots every ref-struct-backed row before returning across awaits.
public static class MessageSendTargetResolver
{
    /// <summary>
    /// <paramref name="requestBytes"/> must be a `messages.sendMessage`-shaped
    /// request, because the forum topic is read out of its reply_to. A caller whose
    /// request has a different layout (forwardMessages names the topic through
    /// top_msg_id) resolves the topic itself and passes
    /// <paramref name="explicitForumTopicId"/> instead.
    /// </summary>
    public static async Task<PreparedMessageTarget> PrepareAsync(
        IChatRepository chatRepository,
        IChatParticipantsRepository chatParticipantsRepository,
        IForumTopicsRepository forumTopicsRepository,
        IMessagingSettingsRepository messagingSettingsRepository,
        long userId, TLPeer.PeerType peerType, long peerId, byte[] requestBytes,
        IReadOnlyCollection<ChatBannedAction> bannedActions,
        int? explicitForumTopicId = null,
        DialogPeerKey? explicitSender = null,
        bool hasExplicitSender = false)
    {
        if (!hasExplicitSender && requestBytes.Length > 0)
        {
            using var request = new TLBytes(requestBytes, 0, requestBytes.Length);
            var send = (SendMessage)request;
            if (send.Flags[13])
            {
                hasExplicitSender = true;
                explicitSender = PeerResolver.ResolveOptionalDialogPeer(
                    send.Get_SendAsView(), userId);
            }
        }

        PreparedMessageTarget target = peerType switch
        {
            TLPeer.PeerType.PeerUser when peerId > 0 => new PreparedMessageTarget(
                peerType, peerId, default,
                peerId == userId ? new[] { userId } : new[] { userId, peerId },
                null, false, 0, null, null),
            TLPeer.PeerType.PeerChat => await PrepareBasicGroupAsync(chatRepository,
                chatParticipantsRepository, userId, peerId, bannedActions),
            TLPeer.PeerType.PeerChannel => await PrepareChannelAsync(chatRepository,
                chatParticipantsRepository, forumTopicsRepository, userId, peerId,
                requestBytes, bannedActions, explicitForumTopicId),
            _ => Error(peerType, peerId, "PEER_ID_INVALID"),
        };
        if (target.Error != null)
        {
            return target;
        }

        SendAsResolver.Resolution sender = await SendAsResolver.ResolveAsync(
            messagingSettingsRepository, chatParticipantsRepository, chatRepository,
            userId, new DialogPeerKey(peerType, peerId),
            hasExplicitSender, explicitSender);
        return sender.Error == null
            ? target with { Sender = sender.Sender }
            : target with { Error = sender.Error };
    }

    private static async Task<PreparedMessageTarget> PrepareBasicGroupAsync(
        IChatRepository chatRepository,
        IChatParticipantsRepository chatParticipantsRepository,
        long userId, long chatId,
        IReadOnlyCollection<ChatBannedAction> bannedActions)
    {
        byte[] chatBytes;
        using (TLChat? storedChat = await chatRepository.GetChatAsync(chatId))
        {
            if (storedChat == null || storedChat.Value.Type != TLChat.ChatType.Chat ||
                storedChat.Value.AsChat().Deactivated)
            {
                return Error(TLPeer.PeerType.PeerChat, chatId, "CHAT_ID_INVALID");
            }
            chatBytes = storedChat.Value.AsSpan().ToArray();
        }

        bool isAdmin;
        using (TLChatParticipantInfo? participant = await chatParticipantsRepository
                   .GetParticipantAsync(chatId, userId))
        {
            if (participant == null || !IsActiveParticipant(participant.Value))
            {
                return Error(TLPeer.PeerType.PeerChat, chatId,
                    "USER_NOT_PARTICIPANT");
            }
            int role = participant.Value.AsChatParticipantInfo().Role;
            isAdmin = role is (int)ChatParticipantRole.Creator or
                (int)ChatParticipantRole.Admin;
        }

        if (!isAdmin && FirstBannedAction(null, chatBytes, bannedActions) is { } denied)
        {
            return Error(TLPeer.PeerType.PeerChat, chatId, ForbiddenError(denied));
        }

        IReadOnlyCollection<TLChatParticipantInfo> participants =
            await chatParticipantsRepository.GetParticipantsAsync(chatId);
        var participantIds = new List<long>();
        foreach (TLChatParticipantInfo participant in participants)
        {
            using var row = participant;
            if (IsActiveParticipant(row))
            {
                participantIds.Add(row.AsChatParticipantInfo().UserId);
            }
        }
        return new PreparedMessageTarget(TLPeer.PeerType.PeerChat, chatId,
            default, participantIds, chatBytes, false, 0, null, null);
    }

    private static async Task<PreparedMessageTarget> PrepareChannelAsync(
        IChatRepository chatRepository,
        IChatParticipantsRepository chatParticipantsRepository,
        IForumTopicsRepository forumTopicsRepository,
        long userId, long channelId, byte[] requestBytes,
        IReadOnlyCollection<ChatBannedAction> bannedActions,
        int? explicitForumTopicId)
    {
        bool broadcast;
        bool forum;
        byte[] channelBytes;
        using (TLChat? channel = await chatRepository.GetChatAsync(channelId))
        {
            if (channel == null || channel.Value.Type != TLChat.ChatType.Channel)
            {
                return Error(TLPeer.PeerType.PeerChannel, channelId,
                    "CHANNEL_INVALID");
            }
            var concrete = channel.Value.AsChannel();
            broadcast = concrete.Broadcast;
            forum = concrete.Forum;
            channelBytes = channel.Value.AsSpan().ToArray();
        }

        bool canManageTopics = false;
        using (TLChatParticipantInfo? participant = await chatParticipantsRepository
                   .GetParticipantAsync(channelId, userId))
        {
            if (participant == null || !IsActiveParticipant(participant.Value))
            {
                return Error(TLPeer.PeerType.PeerChannel, channelId,
                    "CHAT_WRITE_FORBIDDEN");
            }
            if (broadcast)
            {
                if (!ChatRights.HasAdminRight(participant.Value,
                        ChatAdminRightRequirement.PostMessages))
                {
                    return Error(TLPeer.PeerType.PeerChannel, channelId,
                        "CHAT_WRITE_FORBIDDEN");
                }
            }
            else
            {
                bool isAdmin = ChatRights.HasAdminRight(participant.Value,
                    ChatAdminRightRequirement.Any);
                canManageTopics = ChatRights.HasAdminRight(participant.Value,
                    ChatAdminRightRequirement.ManageTopics);
                ChatBannedAction? denied = FirstBannedAction(participant.Value,
                    isAdmin ? null : channelBytes, bannedActions);
                if (denied != null)
                {
                    return Error(TLPeer.PeerType.PeerChannel, channelId,
                        ForbiddenError(denied.Value));
                }
            }
        }

        int forumTopicId = 0;
        StoredMessageForumTopic? forumTopic = null;
        if (forum)
        {
            if (explicitForumTopicId is { } requested)
            {
                forumTopicId = requested;
            }
            else
            {
                using var request = new TLBytes(requestBytes, 0, requestBytes.Length);
                forumTopicId = ForumMessages.ResolveRequestedForumTopicId(request);
            }
            using TLForumTopicInfo? storedTopic = await forumTopicsRepository
                .GetTopicAsync(channelId, forumTopicId);
            if (storedTopic == null)
            {
                return Error(TLPeer.PeerType.PeerChannel, channelId,
                    "TOPIC_ID_INVALID");
            }
            forumTopic = ForumMessages.SnapshotMessageForumTopic(storedTopic.Value);
            if (forumTopic.Closed && !canManageTopics && forumTopic.CreatorId != userId)
            {
                return Error(TLPeer.PeerType.PeerChannel, channelId,
                    "TOPIC_CLOSED");
            }
        }

        return new PreparedMessageTarget(TLPeer.PeerType.PeerChannel, channelId,
            default, new[] { userId }, channelBytes, broadcast, forumTopicId,
            forumTopic, null);
    }

    private static ChatBannedAction? FirstBannedAction(
        TLChatParticipantInfo? participant, byte[]? defaultRightsChatBytes,
        IReadOnlyCollection<ChatBannedAction> actions)
    {
        int now = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
        foreach (ChatBannedAction action in actions)
        {
            if ((participant != null && ChatRights.IsRestrictedFrom(
                    participant.Value, action, now)) ||
                (defaultRightsChatBytes != null && ChatRights.DefaultBans(
                    defaultRightsChatBytes, action)))
            {
                return action;
            }
        }
        return null;
    }

    private static string ForbiddenError(ChatBannedAction action) => action switch
    {
        ChatBannedAction.SendPhotos => ErrorMessages.ChatSendPhotosForbidden.Message,
        ChatBannedAction.SendDocuments => ErrorMessages.ChatSendDocsForbidden.Message,
        _ => "CHAT_WRITE_FORBIDDEN",
    };

    private static bool IsActiveParticipant(TLChatParticipantInfo participant)
    {
        int role = participant.AsChatParticipantInfo().Role;
        return role != (int)ChatParticipantRole.Banned &&
               role != (int)ChatParticipantRole.Left;
    }

    private static PreparedMessageTarget Error(TLPeer.PeerType peerType, long peerId,
        string error) => new(peerType, peerId, default, Array.Empty<long>(), null,
        false, 0, null, error);
}
