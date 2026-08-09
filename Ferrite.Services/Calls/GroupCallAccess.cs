// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Calls;

// Which stored chat row hosts a group call. Ferrite serves calls for basic groups,
// megagroups, and broadcast channels; every other peer kind is rejected before any
// call state is touched.
public enum GroupCallPeerKind
{
    Unsupported,
    BasicGroup,
    Megagroup,
    Broadcast,
}

// The gate each group-call endpoint runs. Read covers the call/participant reads,
// Participate covers the self-service endpoints (join/leave/own settings), and
// Manage covers the creator/manage_call operations (create, discard, title,
// join-muted, moderation, invite links).
public enum GroupCallAccessLevel
{
    Read,
    Participate,
    Manage,
}

// The hosting peer of a call, in the dto row's own peer_type numbering.
public readonly record struct GroupCallPeerRef(GroupCallPeerType Type, long Id);

// Result of the gate. Error is null on success; every other member is only
// meaningful then. ChatBytes is a copy of the stored compact chat/channel row so
// callers can build linkage and updates without a second read.
//
// A peerless E2E conference has no hosting chat at all, so it is authorized
// against its own participant list instead and reports IsConference. Kind stays
// Unsupported and ChatBytes stays null for such a call: a handler that would
// have unlinked a chat or written a chat action must take its conference path
// rather than dereference either.
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

// The error strings the group-call surface returns. Kept here so every // endpoint reports the same wording for the same condition.
public static class GroupCallErrors
{
    public const string AuthKeyInvalid = "AUTH_KEY_INVALID";
    public const string PeerIdInvalid = "PEER_ID_INVALID";
    public const string UserNotParticipant = "USER_NOT_PARTICIPANT";
    public const string ChatAdminRequired = "CHAT_ADMIN_REQUIRED";

    // The call itself is unknown, its access hash does not match, or the client
    // sent an InputGroupCall variant Ferrite does not serve. Pinned TDLib treats
    // this as "the call is gone" and drops its local state.
    public const string GroupCallInvalid = "GROUPCALL_INVALID";

    // The call exists but this account may not see it, which is what a
    // call-scoped endpoint reports instead of leaking chat membership.
    public const string GroupCallForbidden = "GROUPCALL_FORBIDDEN";

    // One peer hosts at most one non-discarded call.
    public const string GroupCallAlreadyStarted = "GROUPCALL_ALREADY_STARTED";

    public const string ScheduleDateInvalid = "SCHEDULE_DATE_INVALID";
    public const string TitleInvalid = "TITLE_INVALID";

    // The external media worker could not allocate the room, so no call row is
    // written at all rather than leaving a call nobody can join.
    public const string MediaUnavailable = "GROUPCALL_MEDIA_UNAVAILABLE";

    // The account has no active (non-left) participant row in this call. Pinned
    // TDLib reports this for leave/presentation operations issued without a join.
    public const string GroupCallJoinMissing = "GROUPCALL_JOIN_MISSING";

    // Another active participant already owns the requested SSRC, so the join is
    // refused rather than letting two participants collide on the media plane.
    public const string SsrcDuplicateMuch = "GROUPCALL_SSRC_DUPLICATE_MUCH";

    // The join/presentation params blob is not a payload the tgcalls contract
    // allows.
    public const string DataJsonInvalid = "DATA_JSON_INVALID";

    // Ferrite serves the account's own identity only. Anonymous/channel join-as
    // remains outside the current supported boundary.
    public const string JoinAsPeerInvalid = "JOIN_AS_PEER_INVALID";

    public const string PublicChannelMissing = "PUBLIC_CHANNEL_MISSING";
    public const string UserIdInvalid = "USER_ID_INVALID";
    public const string UsersTooMuch = "USERS_TOO_MUCH";
    public const string UserAlreadyInvited = "USER_ALREADY_INVITED";

    // The invite hash is unknown, revoked, expired, or belongs to another call.
    public const string InviteHashExpired = "INVITE_HASH_EXPIRED";

    // The tde2e block did not parse, was signed by the wrong key, or claims a
    // state the chain rejects. The client must not retry it unchanged.
    public const string BlockInvalid = "BLOCK_INVALID";

    // Another client already took this height. The loser refetches the head and
    // rebuilds on it; this is the fork prevention the whole chain depends on.
    public const string BlockHeightMismatch = "BLOCK_HEIGHT_MISMATCH";

    // The block's prev_block_hash does not name the current head.
    public const string BlockHashMismatch = "BLOCK_HASH_MISMATCH";

    // editGroupCallParticipant named an account that has no active row in the
    // call, or an InputPeer shape that cannot name a served participant.
    public const string ParticipantIdInvalid = "PARTICIPANT_ID_INVALID";

    // Participant volume is 1..20000 (pinned TDLib's MIN/MAX_VOLUME_LEVEL) and
    // never applies to the invoker's own row.
    public const string VolumeInvalid = "VOLUME_INVALID";

    // The not-modified answer for call-only settings. Pinned TDLib treats this
    // exact error as SUCCESS for editGroupCallTitle and toggleGroupCallSettings
    // (GroupCallManager.cpp on_error handlers), so an idempotent retry resolves
    // cleanly instead of surfacing an error to the app layer.
    public const string GroupCallNotModified = "GROUPCALL_NOT_MODIFIED";
}

public static class GroupCallAccess
{
    // Resolves the call-hosting peer from a request's InputPeer. User/self/empty
    // peers cannot host a group call and are rejected by the caller as
    // PEER_ID_INVALID. InputPeerView is a ref struct, so callers resolve the peer
    // synchronously before any await.
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

    // The stored row must agree with the call row's peer_type: a chat-typed call
    // never resolves against a channel row and vice versa. Forbidden and
    // deactivated rows cannot host a call at all.
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
