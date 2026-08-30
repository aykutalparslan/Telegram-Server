// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Messages;

public static class MessageEditRules
{
    public const int EditTimeLimitSeconds = 172800;

    public static bool IsAuthoredBy(Message message, long userId) =>
        message.Flags[8] &&
        PeerResolver.TryReadPeer(message.Get_FromIdView(), out var from) &&
        from.Type == TLPeer.PeerType.PeerUser && from.Id == userId;

    public static bool IsExpired(Message message, int now, bool exempt) =>
        !exempt && now - (long)message.Date > EditTimeLimitSeconds;

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
