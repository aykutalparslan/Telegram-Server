// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Numerics;

namespace Ferrite.Crypto;

/// <summary>
/// Shared Telegram Diffie-Hellman parameters used by MTProto and secret chats.
/// </summary>
public static class TelegramDhParameters
{
    public const int SecretChatGenerator = 3;
    public const int SecretChatVersion = 2;

    private static readonly byte[] s_prime = Convert.FromHexString(
        "C71CAEB9C6B1C9048E6C522F70F13F73980D40238E3E21C14934D037563D930F" +
        "48198A0AA7C14058229493D22530F4DBFA336F6E0AC925139543AED44CCE7C372" +
        "0FD51F69458705AC68CD4FE6B6B13ABDC9746512969328454F18FAF8C595F642" +
        "477FE96BB2A941D5BCD1D4AC8CC49880708FA9B378E3C4F3A9060BEE67CF9A4A" +
        "4A695811051907E162753B56B0F6B410DBA74D8A84B2A14B3144E0EF1284754F" +
        "D17ED950D5965B4B9DD46582DB1178D169C6BC465B0D6FF9CA3928FEF5B9AE4E" +
        "418FC15E83EBEA0F87FA9FF5EED70050DED2849F47BF959D956850CE929851F0" +
        "D8115F635B105EE2E4E15D04B2454BF6F4FADF034B10403119CD8E3B92FCC5B");

    private static readonly BigInteger s_secretChatLowerBound =
        BigInteger.One << 1984;
    private static readonly BigInteger s_secretChatUpperBound =
        new BigInteger(s_prime, isUnsigned: true, isBigEndian: true) -
        s_secretChatLowerBound;

    public static ReadOnlySpan<byte> Prime => s_prime;

    /// <summary>
    /// Validates a client secret-chat public value against Telegram's strong
    /// interval, inclusive at both boundaries.
    /// </summary>
    public static bool IsValidSecretChatPublicValue(ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty)
        {
            return false;
        }

        var publicValue = new BigInteger(value, isUnsigned: true,
            isBigEndian: true);
        return publicValue >= s_secretChatLowerBound &&
               publicValue <= s_secretChatUpperBound;
    }
}
