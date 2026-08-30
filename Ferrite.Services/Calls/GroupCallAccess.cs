// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Calls;

public enum GroupCallPeerKind
{
    Unsupported,
    BasicGroup,
    Megagroup,
    Broadcast,
}

public enum GroupCallAccessLevel
{
    Read,
    Participate,
    Manage,
}

public readonly record struct GroupCallPeerRef(GroupCallPeerType Type, long Id);

public sealed record GroupCallPeerAccess(long CurrentUserId, GroupCallPeerKind Kind,
    GroupCallPeerRef Peer, bool IsCreator, bool CanManageCall, byte[]? ChatBytes,
    string? Error, bool IsConference = false)
{
    public static GroupCallPeerAccess Failed(string error) =>
        new(0, GroupCallPeerKind.Unsupported, default, false, false, null, error);

    public static GroupCallPeerAccess Conference(long currentUserId, long creatorUserId,
        bool isCreator) =>
        new(currentUserId, GroupCallPeerKind.Unsupported,
            new GroupCallPeerRef(GroupCallPeerType.None, creatorUserId), isCreator,
            isCreator, null, null, IsConference: true);
}

public static class GroupCallErrors
{
    public const string AuthKeyInvalid = "AUTH_KEY_INVALID";
    public const string PeerIdInvalid = "PEER_ID_INVALID";
    public const string UserNotParticipant = "USER_NOT_PARTICIPANT";
    public const string ChatAdminRequired = "CHAT_ADMIN_REQUIRED";

    public const string GroupCallInvalid = "GROUPCALL_INVALID";

    public const string GroupCallForbidden = "GROUPCALL_FORBIDDEN";

    public const string GroupCallAlreadyStarted = "GROUPCALL_ALREADY_STARTED";

    public const string ScheduleDateInvalid = "SCHEDULE_DATE_INVALID";
    public const string TitleInvalid = "TITLE_INVALID";

    public const string MediaUnavailable = "GROUPCALL_MEDIA_UNAVAILABLE";

    public const string GroupCallJoinMissing = "GROUPCALL_JOIN_MISSING";

    public const string SsrcDuplicateMuch = "GROUPCALL_SSRC_DUPLICATE_MUCH";

    public const string DataJsonInvalid = "DATA_JSON_INVALID";

    public const string JoinAsPeerInvalid = "JOIN_AS_PEER_INVALID";

    public const string PublicChannelMissing = "PUBLIC_CHANNEL_MISSING";
    public const string UserIdInvalid = "USER_ID_INVALID";
    public const string UsersTooMuch = "USERS_TOO_MUCH";
    public const string UserAlreadyInvited = "USER_ALREADY_INVITED";

    public const string InviteHashExpired = "INVITE_HASH_EXPIRED";

    public const string BlockInvalid = "BLOCK_INVALID";

    public const string BlockHeightMismatch = "BLOCK_HEIGHT_MISMATCH";

    public const string BlockHashMismatch = "BLOCK_HASH_MISMATCH";

    public const string ParticipantIdInvalid = "PARTICIPANT_ID_INVALID";

    public const string VolumeInvalid = "VOLUME_INVALID";

    public const string GroupCallNotModified = "GROUPCALL_NOT_MODIFIED";
}

public static class GroupCallAccess
{
    public static bool TryResolveCallPeer(InputPeerView peer, out GroupCallPeerRef value)
    {
        if (peer.Is(out InputPeerChat chat) && chat.ChatId > 0)
        {
            value = new GroupCallPeerRef(GroupCallPeerType.Chat, chat.ChatId);
            return true;
        }
        if (peer.Is(out InputPeerChannel channel) && channel.ChannelId > 0)
        {
            value = new GroupCallPeerRef(GroupCallPeerType.Channel, channel.ChannelId);
            return true;
        }
        if (peer.Is(out InputPeerChannelFromMessage fromMessage) &&
            fromMessage.ChannelId > 0)
        {
            value = new GroupCallPeerRef(GroupCallPeerType.Channel, fromMessage.ChannelId);
            return true;
        }

        value = default;
        return false;
    }

    public static async ValueTask<GroupCallPeerAccess> AuthorizeAsync(
        IAuthorizationRepository authorizationRepository,
        IChatRepository chatRepository,
        IChatParticipantsRepository chatParticipantsRepository,
        long authKeyId, GroupCallPeerRef peer, GroupCallAccessLevel level,
        CancellationToken cancellationToken = default)
    {
        var auth = await authorizationRepository.GetAuthorizationAsync(authKeyId);
        if (auth == null)
        {
            return GroupCallPeerAccess.Failed(GroupCallErrors.AuthKeyInvalid);
        }

        long currentUserId = auth.Value.AsAuthInfo().UserId;
        if (peer.Id <= 0)
        {
            return GroupCallPeerAccess.Failed(GroupCallErrors.PeerIdInvalid);
        }

        using var chatRow = await chatRepository.GetChatAsync(peer.Id);
        if (chatRow == null)
        {
            return GroupCallPeerAccess.Failed(GroupCallErrors.PeerIdInvalid);
        }

        var kind = ResolveKind(chatRow.Value, peer.Type);
        if (kind == GroupCallPeerKind.Unsupported)
        {
            return GroupCallPeerAccess.Failed(GroupCallErrors.PeerIdInvalid);
        }

        using var participant = await chatParticipantsRepository
            .GetParticipantAsync(peer.Id, currentUserId);
        if (participant == null || !IsActiveParticipant(participant.Value))
        {
            return GroupCallPeerAccess.Failed(GroupCallErrors.UserNotParticipant);
        }

        bool isCreator = participant.Value.AsChatParticipantInfo().Role ==
                         (int)ChatParticipantRole.Creator;
        bool canManageCall = ChatRights.HasAdminRight(participant.Value,
            ChatAdminRightRequirement.ManageCall);
        if (level == GroupCallAccessLevel.Manage && !canManageCall)
        {
            return GroupCallPeerAccess.Failed(GroupCallErrors.ChatAdminRequired);
        }

        return new GroupCallPeerAccess(currentUserId, kind, peer, isCreator, canManageCall,
            chatRow.Value.AsSpan().ToArray(), null);
    }

    private static GroupCallPeerKind ResolveKind(TLChat stored, GroupCallPeerType peerType)
    {
        if (peerType == GroupCallPeerType.Chat)
        {
            if (stored.Type != TLChat.ChatType.Chat || stored.AsChat().Deactivated)
            {
                return GroupCallPeerKind.Unsupported;
            }

            return GroupCallPeerKind.BasicGroup;
        }

        if (stored.Type != TLChat.ChatType.Channel)
        {
            return GroupCallPeerKind.Unsupported;
        }

        return stored.AsChannel().Megagroup
            ? GroupCallPeerKind.Megagroup
            : GroupCallPeerKind.Broadcast;
    }

    private static bool IsActiveParticipant(TLChatParticipantInfo participant)
    {
        int role = participant.AsChatParticipantInfo().Role;
        return role != (int)ChatParticipantRole.Banned &&
               role != (int)ChatParticipantRole.Left;
    }
}
