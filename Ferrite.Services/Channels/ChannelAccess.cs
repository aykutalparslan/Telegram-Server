// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Channels;

public static class ChannelAccess
{
    public static async ValueTask<string?> ValidateReadAsync(
        IChatRepository chatRepository,
        IChatParticipantsRepository chatParticipantsRepository,
        long channelId, long userId)
    {
        using (TLChat? channel = await chatRepository
                   .GetChatAsync(channelId))
        {
            if (channel == null || channel.Value.Type != TLChat.ChatType.Channel)
            {
                return "CHANNEL_INVALID";
            }
        }

        using TLChatParticipantInfo? participant = await chatParticipantsRepository
            .GetParticipantAsync(channelId, userId);
        if (participant == null)
        {
            return "CHANNEL_PRIVATE";
        }

        int role = participant.Value.AsChatParticipantInfo().Role;
        return role is (int)ChatParticipantRole.Banned or (int)ChatParticipantRole.Left
            ? "CHANNEL_PRIVATE"
            : null;
    }
}
