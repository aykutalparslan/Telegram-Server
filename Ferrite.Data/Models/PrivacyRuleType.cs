// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Data.Models;

public enum PrivacyRuleType
{
    AllowContacts,
    AllowAll,
    AllowUsers,
    DisallowContacts,
    DisallowAll,
    DisallowUsers,
    AllowChatParticipants,
    DisallowChatParticipants,
    AllowCloseFriends,
    AllowPremium,
    AllowBots,
    DisallowBots,
}
