// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Security.Cryptography;

namespace Ferrite.Services.Calls;

/// <summary>
/// Builds the public deep links exported by phone.exportGroupCallInvite. A link
/// without a hash is listen-only; a hash is a dedicated group-call credential
/// whose generation, revocation, and expiry are validated by joinGroupCall.
/// </summary>
public static class GroupCallInviteLinks
{
    private const int HashBytes = 18;

    public static string GenerateHash()
    {
        string encoded = Convert.ToBase64String(RandomNumberGenerator.GetBytes(HashBytes));
        return encoded.TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static string Build(string username, bool liveStream, string? inviteHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        string kind = liveStream ? "livestream" : "videochat";
        return inviteHash == null
            ? $"https://t.me/{username}?{kind}"
            : $"https://t.me/{username}?{kind}={inviteHash}";
    }
}
