// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Org.BouncyCastle.Math.EC.Rfc8032;

namespace Ferrite.Crypto;

// RFC 8032 Ed25519 over the raw message, matching
// td::Ed25519::PublicKey::verify_signature, which tde2e uses for conference
// chain blocks and sub-chain broadcasts.
public static class Ed25519Verifier
{
    public const int PublicKeySize = 32;
    public const int SignatureSize = 64;

    public static bool Verify(ReadOnlySpan<byte> publicKey, ReadOnlySpan<byte> message,
        ReadOnlySpan<byte> signature)
    {
        if (publicKey.Length != PublicKeySize) return false;
        if (signature.Length != SignatureSize) return false;
        try
        {
            return Ed25519.Verify(signature.ToArray(), 0, publicKey.ToArray(), 0,
                message.ToArray(), 0, message.Length);
        }
        catch (Exception)
        {
            // A malformed point or a non-canonical encoding is a rejected
            // signature, not a server fault.
            return false;
        }
    }
}
