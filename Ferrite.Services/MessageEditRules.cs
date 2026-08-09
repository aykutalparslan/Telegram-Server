// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services;

/// <summary>
/// The rules that decide whether a stored message may be edited at all, shared by
/// <c>messages.getMessageEditData</c> and <c>messages.editMessage</c> so the
/// question a client asks and the answer it later gets cannot diverge.
/// </summary>
public static class MessageEditRules
{
    /// <summary>Telegram's 48-hour ordinary edit window.</summary>
    public const int EditTimeLimitSeconds = 172800;

    public static bool IsAuthoredBy(Message message, long userId) =>
        message.Flags[8] &&
        PeerResolver.TryReadPeer(message.Get_FromIdView(), out var from) &&
        from.Type == TLPeer.PeerType.PeerUser && from.Id == userId;

    public static bool IsExpired(Message message, int now, bool exempt) =>
        !exempt && now - (long)message.Date > EditTimeLimitSeconds;

    /// <summary>
    /// Broadcast admins may edit posts they did not author. The creator always
    /// qualifies; an admin row without an explicit rights value is a legacy
    /// full-rights row, which is how the rest of the codebase reads it.
    /// </summary>
    public static bool HasEditMessagesRight(TLChatParticipantInfo participant)
    {
        var info = participant.AsChatParticipantInfo();
        if (info.Role == (int)ChatParticipantRole.Creator)
        {
            return true;
        }
        if (info.Role != (int)ChatParticipantRole.Admin)
        {
            return false;
        }
        return !info.Flags[0] ||
               info.Get_AdminRightsView().Is(out ChatAdminRights rights) &&
               rights.EditMessages;
    }

    public static bool IsActiveParticipant(TLChatParticipantInfo participant)
    {
        int role = participant.AsChatParticipantInfo().Role;
        return role != (int)ChatParticipantRole.Banned &&
               role != (int)ChatParticipantRole.Left;
    }
}
