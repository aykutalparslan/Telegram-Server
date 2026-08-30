// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Org.BouncyCastle.Math.EC.Rfc8032;

namespace Ferrite.Crypto;

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
            return false;
        }
    }
}
