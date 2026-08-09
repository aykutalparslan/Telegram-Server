// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services;

/// <summary>
/// The peer preflight every scheduled-queue method shares. A queue is keyed by its
/// own owner, so no caller can reach another user's entries by naming a peer; what
/// this check adds is that the addressed dialog exists and is still one the caller
/// participates in, so a stale client is answered with the protocol error rather
/// than with an empty queue for a chat it was removed from.
/// </summary>
public static class ScheduledQueueAccess
{
    public readonly record struct Resolved(long UserId, TLPeer.PeerType PeerType,
        long PeerId, ErrorMessage? Error)
    {
        public static Resolved Fail(int code, string message) =>
            new(0, default, 0, new ErrorMessage(code, message));
    }

    /// <summary>
    /// The logged-in principal behind an auth key, resolved before any request view
    /// is parsed: an `InputPeerView` is a ref struct and cannot cross this await.
    /// </summary>
    public static async Task<long?> AuthenticateAsync(
        IAuthorizationRepository authorizationRepository,
        long authKeyId)
    {
        using TLAuthInfo? auth = await authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        return auth == null || !auth.Value.AsAuthInfo().LoggedIn
            ? null
            : auth.Value.AsAuthInfo().UserId;
    }

    public static async Task<Resolved> ValidateAsync(IUserRepository userRepository,
        IChatRepository chatRepository,
        IChatParticipantsRepository chatParticipantsRepository,
        long userId, DialogPeerKey? peer)
    {
        if (peer is not { } key || key.Id <= 0)
        {
            return Resolved.Fail(400, "PEER_ID_INVALID");
        }

        switch (key.Type)
        {
            case TLPeer.PeerType.PeerUser:
            {
                using TLUser? user = userRepository.GetUser(key.Id);
                return user == null
                    ? Resolved.Fail(400, "PEER_ID_INVALID")
                    : new Resolved(userId, key.Type, key.Id, null);
            }
            case TLPeer.PeerType.PeerChat:
            {
                using (TLChat? chat = await chatRepository
                           .GetChatAsync(key.Id))
                {
                    if (chat == null || chat.Value.Type != TLChat.ChatType.Chat ||
                        chat.Value.AsChat().Deactivated)
                    {
                        return Resolved.Fail(400, "CHAT_ID_INVALID");
                    }
                }
                return await CheckParticipantAsync(chatParticipantsRepository,
                    userId, key,
                    "USER_NOT_PARTICIPANT");
            }
            case TLPeer.PeerType.PeerChannel:
            {
                using (TLChat? channel = await chatRepository
                           .GetChatAsync(key.Id))
                {
                    if (channel == null ||
                        channel.Value.Type != TLChat.ChatType.Channel)
                    {
                        return Resolved.Fail(400, "CHANNEL_INVALID");
                    }
                }
                return await CheckParticipantAsync(chatParticipantsRepository,
                    userId, key,
                    "CHANNEL_PRIVATE");
            }
            default:
                return Resolved.Fail(400, "PEER_ID_INVALID");
        }
    }

    private static async Task<Resolved> CheckParticipantAsync(
        IChatParticipantsRepository chatParticipantsRepository,
        long userId, DialogPeerKey key, string error)
    {
        using TLChatParticipantInfo? participant = await chatParticipantsRepository
            .GetParticipantAsync(key.Id, userId);
        return participant == null ||
               !MessageEditRules.IsActiveParticipant(participant.Value)
            ? Resolved.Fail(400, error)
            : new Resolved(userId, key.Type, key.Id, null);
    }
}
