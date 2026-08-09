// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services;

/// <summary>
/// Outcome of evaluating whether a caller may start a phone call with a
/// target user. Block state takes precedence over stored privacy rules.
/// </summary>
public enum CallPrivacyDecision
{
    Allowed,
    Blocked,
    PrivacyRestricted,
}
