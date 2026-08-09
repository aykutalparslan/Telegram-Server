// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Gateway;

public enum SentCodeType
{
    Sms,
    Call,
    FlashCall,
    MissedCall,
    App,
    Email,
}