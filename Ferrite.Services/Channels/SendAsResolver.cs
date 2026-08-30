// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Channels;

public static class SendAsResolver
{
    public readonly record struct Resolution(DialogPeerKey Sender, string? Error);

    public static async ValueTask<bool> CanAddressAsync(IUserRepository userRepository,
        IChatRepository chatRepository,
        IChatParticipantsRepository chatParticipantsRepository,
        TimeProvider timeProvider, long userId, DialogPeerKey destination)
    {
        if (destination.Id <= 0)
        {
            return false;
        }
        if (destination.Type == TLPeer.PeerType.PeerUser)
        {
            using TLUser? user = userRepository.GetUser(destination.Id);
            return user != null;
        }

        using TLChat? chat = await chatRepository
            .GetChatAsync(destination.Id);
        if (chat == null)
        {
            return false;
        }
        using TLChatParticipantInfo? participant = await chatParticipantsRepository
            .GetParticipantAsync(destination.Id, userId);
        if (participant == null || !IsActive(participant.Value))
        {
            return false;
        }
        if (destination.Type == TLPeer.PeerType.PeerChat)
        {
            return chat.Value.Type == TLChat.ChatType.Chat;
        }
        if (chat.Value.Type != TLChat.ChatType.Channel)
        {
            return false;
        }

        var channel = chat.Value.AsChannel();
        if (channel.Broadcast)
        {
            return ChatRights.HasAdminRight(participant.Value,
                ChatAdminRightRequirement.PostMessages);
        }

        int now = checked((int)timeProvider.GetUtcNow().ToUnixTimeSeconds());
        return !ChatRights.IsRestrictedFrom(participant.Value,
                   ChatBannedAction.SendMessages, now) &&
               !ChatRights.DefaultBans(chat.Value.AsSpan().ToArray(),
                   ChatBannedAction.SendMessages);
    }

    public static async ValueTask<List<long>> GetOwnedSenderChannelIdsAsync(
        IChatParticipantsRepository chatParticipantsRepository, long userId)
    {
        IReadOnlyCollection<TLChatParticipantInfo> memberships =
            await chatParticipantsRepository.GetParticipantsByUserAsync(userId);
        var candidates = new List<long>();
        var seen = new HashSet<long>();
        foreach (TLChatParticipantInfo membership in memberships)
        {
            using (membership)
            {
                var info = membership.AsChatParticipantInfo();
                if (info.Role is not ((int)ChatParticipantRole.Creator) and
                    not ((int)ChatParticipantRole.Admin))
                {
                    continue;
                }
                if (info.Role == (int)ChatParticipantRole.Admin &&
                    !ChatRights.HasAdminRight(membership,
                        ChatAdminRightRequirement.PostMessages))
                {
                    continue;
                }
                if (seen.Add(info.ChatId))
                {
                    candidates.Add(info.ChatId);
                }
            }
        }
        return candidates;
    }

    public static async ValueTask<bool> IsChannelAsync(IChatRepository chatRepository,
        long chatId)
    {
        using TLChat? chat = await chatRepository.GetChatAsync(chatId);
        return chat != null && chat.Value.Type == TLChat.ChatType.Channel;
    }

    public static async ValueTask<bool> IsAllowedSenderAsync(
        IChatParticipantsRepository chatParticipantsRepository,
        IChatRepository chatRepository, long userId, DialogPeerKey sender)
    {
        if (sender.Id <= 0)
        {
            return false;
        }
        if (sender.Type == TLPeer.PeerType.PeerUser)
        {
            return sender.Id == userId;
        }
        if (sender.Type != TLPeer.PeerType.PeerChannel)
        {
            return false;
        }

        List<long> candidates = await GetOwnedSenderChannelIdsAsync(
            chatParticipantsRepository, userId);
        return candidates.Contains(sender.Id) &&
               await IsChannelAsync(chatRepository, sender.Id);
    }

    public static async ValueTask<Resolution> ResolveAsync(
        IMessagingSettingsRepository messagingSettingsRepository,
        IChatParticipantsRepository chatParticipantsRepository,
        IChatRepository chatRepository, long userId, DialogPeerKey destination,
        bool hasExplicitSender,
        DialogPeerKey? explicitSender)
    {
        var self = new DialogPeerKey(TLPeer.PeerType.PeerUser, userId);
        if (destination.Type != TLPeer.PeerType.PeerChannel)
        {
            return hasExplicitSender
                ? new Resolution(default, "SEND_AS_PEER_INVALID")
                : new Resolution(self, null);
        }
        if (hasExplicitSender && explicitSender == null)
        {
            return new Resolution(default, "SEND_AS_PEER_INVALID");
        }

        DialogPeerKey? selected = explicitSender;
        if (!hasExplicitSender)
        {
            using TLDefaultSendAs? stored = await messagingSettingsRepository
                .GetDefaultSendAsAsync(userId,
                    (int)destination.Type, destination.Id);
            if (stored != null)
            {
                var row = stored.Value.AsDefaultSendAs();
                selected = new DialogPeerKey(
                    (TLPeer.PeerType)row.SendAsPeerType, row.SendAsPeerId);
            }
        }

        selected ??= self;
        return await IsAllowedSenderAsync(chatParticipantsRepository, chatRepository,
            userId, selected.Value)
            ? new Resolution(selected.Value, null)
            : new Resolution(default, "SEND_AS_PEER_INVALID");
    }

    private static bool IsActive(TLChatParticipantInfo participant)
    {
        int role = participant.AsChatParticipantInfo().Role;
        return role != (int)ChatParticipantRole.Banned &&
               role != (int)ChatParticipantRole.Left;
    }
}
