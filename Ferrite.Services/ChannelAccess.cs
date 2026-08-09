// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services;

/// <summary>
/// The membership check every channel read shares: the peer must be a channel and
/// the caller an active participant of it. Returns the protocol error string, or
/// null when the read may proceed.
/// </summary>
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
