// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.channels;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Handlers.ChannelForums;

internal static class ChannelForumAccess
{
    internal static async Task<(long CurrentUserId, byte[] ChannelBytes,
        byte[] ParticipantBytes, string? Error)> PrepareForumAccessAsync(
        IAuthorizationRepository authorizationRepository,
        IChatRepository chatRepository,
        IChatParticipantsRepository chatParticipantsRepository,
        long authKeyId, long? channelId)
    {
        var auth = await authorizationRepository.GetAuthorizationAsync(authKeyId);
        if (auth == null)
            return (0, Array.Empty<byte>(), Array.Empty<byte>(), "AUTH_KEY_INVALID");
        long userId = auth.Value.AsAuthInfo().UserId;
        if (channelId is not > 0)
            return (0, Array.Empty<byte>(), Array.Empty<byte>(), "CHANNEL_INVALID");
        using var channel = await chatRepository.GetChatAsync(channelId.Value);
        if (channel == null || channel.Value.Type != TLChat.ChatType.Channel ||
            !channel.Value.AsChannel().Megagroup || !channel.Value.AsChannel().Forum)
            return (0, Array.Empty<byte>(), Array.Empty<byte>(), "CHANNEL_INVALID");
        using var participant = await chatParticipantsRepository
            .GetParticipantAsync(channelId.Value, userId);
        if (participant == null || !IsActiveParticipant(participant.Value))
            return (0, Array.Empty<byte>(), Array.Empty<byte>(), "CHANNEL_PRIVATE");
        return (userId, channel.Value.AsSpan().ToArray(),
            participant.Value.AsSpan().ToArray(), null);
    }

    internal static async Task<(long CurrentUserId, byte[] ChannelBytes, string? Error)>
        PrepareForumMutationAsync(IAuthorizationRepository authorizationRepository,
            IChatRepository chatRepository,
            IChatParticipantsRepository chatParticipantsRepository, long authKeyId,
            long? channelId, ChatAdminRightRequirement requiredRight)
    {
        var (userId, channelBytes, participantBytes, error) =
            await PrepareForumAccessAsync(authorizationRepository, chatRepository,
                chatParticipantsRepository, authKeyId, channelId);
        if (error != null) return (0, Array.Empty<byte>(), error);
        return ChatRights.HasAdminRight(participantBytes, requiredRight)
            ? (userId, channelBytes, null)
            : (0, Array.Empty<byte>(), "CHAT_ADMIN_REQUIRED");
    }

    internal static async Task<(long CurrentUserId, byte[] ChannelBytes, string? Error)>
        PrepareChannelMutationAsync(IAuthorizationRepository authorizationRepository,
            IChatRepository chatRepository,
            IChatParticipantsRepository chatParticipantsRepository, long authKeyId,
            long? channelId, bool creatorOnly)
    {
        var auth = await authorizationRepository.GetAuthorizationAsync(authKeyId);
        if (auth == null) return (0, Array.Empty<byte>(), "AUTH_KEY_INVALID");

        long currentUserId = auth.Value.AsAuthInfo().UserId;
        if (channelId is not > 0) return (0, Array.Empty<byte>(), "CHANNEL_INVALID");

        using var channel = await chatRepository.GetChatAsync(channelId.Value);
        if (channel == null || channel.Value.Type != TLChat.ChatType.Channel)
            return (0, Array.Empty<byte>(), "CHANNEL_INVALID");

        using var participant = await chatParticipantsRepository
            .GetParticipantAsync(channelId.Value, currentUserId);
        if (participant == null || !IsActiveParticipant(participant.Value))
            return (0, Array.Empty<byte>(), "USER_NOT_PARTICIPANT");

        bool authorized = !creatorOnly ||
            participant.Value.AsChatParticipantInfo().Role == (int)ChatParticipantRole.Creator;
        return authorized
            ? (currentUserId, channel.Value.AsSpan().ToArray(), null)
            : (0, Array.Empty<byte>(), "CHAT_ADMIN_REQUIRED");
    }

    internal static long? ResolveInputChannelId(InputChannelView channel)
    {
        if (channel.Is(out InputChannel inputChannel)) return inputChannel.ChannelId;
        if (channel.Is(out InputChannelFromMessage fromMessage)) return fromMessage.ChannelId;
        return null;
    }

    private static bool IsActiveParticipant(TLChatParticipantInfo participant)
    {
        int role = participant.AsChatParticipantInfo().Role;
        return role != (int)ChatParticipantRole.Banned &&
               role != (int)ChatParticipantRole.Left;
    }
}
