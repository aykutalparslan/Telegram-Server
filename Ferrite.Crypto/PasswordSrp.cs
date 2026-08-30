// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Numerics;
using System.Security.Cryptography;

namespace Ferrite.Crypto;

public enum PasswordSrpVerificationResult
{
    Success,
    InvalidVerifier,
    InvalidServerSecret,
    InvalidClientPublicValue,
    InvalidClientProof
}

public readonly record struct PasswordSrpChallenge(
    byte[] ServerSecret,
    byte[] PublicValue);

public static class PasswordSrp
{
    public const int PaddedLength = 256;
    public const int ProofLength = 32;
    public const int Generator = TelegramDhParameters.SecretChatGenerator;

    private static readonly BigInteger s_prime = new(
        TelegramDhParameters.Prime, isUnsigned: true, isBigEndian: true);
    private static readonly byte[] s_generatorPadded = PadInteger(Generator);
    private static readonly byte[] s_multiplierHash = Hash(
        TelegramDhParameters.Prime, s_generatorPadded);
    private static readonly BigInteger s_multiplier = new(
        s_multiplierHash, isUnsigned: true, isBigEndian: true);
    private static readonly byte[] s_primeGeneratorHashXor =
        Xor(Hash(TelegramDhParameters.Prime), Hash(s_generatorPadded));

    public static bool IsValidVerifier(ReadOnlySpan<byte> verifier) =>
        IsValidGroupElement(verifier);

    public static PasswordSrpChallenge CreateChallenge(
        ReadOnlySpan<byte> verifier)
    {
        if (!IsValidGroupElement(verifier))
        {
            throw new ArgumentException(
                "The password verifier must be at most 256 bytes and in the range (0, p).",
                nameof(verifier));
        }

        byte[] serverSecret = new byte[PaddedLength];
        while (true)
        {
            RandomNumberGenerator.Fill(serverSecret);
            if (TryCreateChallenge(verifier, serverSecret, out var challenge))
            {
                CryptographicOperations.ZeroMemory(serverSecret);
                return challenge;
            }
        }
    }

    public static bool TryCreateChallenge(
        ReadOnlySpan<byte> verifier,
        ReadOnlySpan<byte> serverSecret,
        out PasswordSrpChallenge challenge)
    {
        challenge = default;
        if (!IsValidGroupElement(verifier) ||
            !IsValidGroupElement(serverSecret))
        {
            return false;
        }

        BigInteger verifierInteger = ToInteger(verifier);
        BigInteger secretInteger = ToInteger(serverSecret);
        BigInteger publicInteger = (
            s_multiplier * verifierInteger +
            BigInteger.ModPow(Generator, secretInteger, s_prime)) % s_prime;
        if (publicInteger <= BigInteger.Zero)
        {
            return false;
        }

        challenge = new PasswordSrpChallenge(
            serverSecret.ToArray(),
            PadInteger(publicInteger));
        return true;
    }

    public static PasswordSrpVerificationResult VerifyProof(
        ReadOnlySpan<byte> verifier,
        ReadOnlySpan<byte> salt1,
        ReadOnlySpan<byte> salt2,
        ReadOnlySpan<byte> serverSecret,
        ReadOnlySpan<byte> clientPublicValue,
        ReadOnlySpan<byte> clientProof)
    {
        if (!IsValidGroupElement(verifier))
        {
            return PasswordSrpVerificationResult.InvalidVerifier;
        }

        if (!IsValidGroupElement(serverSecret))
        {
            return PasswordSrpVerificationResult.InvalidServerSecret;
        }

        if (!IsValidGroupElement(clientPublicValue))
        {
            return PasswordSrpVerificationResult.InvalidClientPublicValue;
        }

        if (clientProof.Length != ProofLength)
        {
            return PasswordSrpVerificationResult.InvalidClientProof;
        }

        BigInteger verifierInteger = ToInteger(verifier);
        BigInteger secretInteger = ToInteger(serverSecret);
        BigInteger clientPublicInteger = ToInteger(clientPublicValue);
        BigInteger serverPublicInteger = (
            s_multiplier * verifierInteger +
            BigInteger.ModPow(Generator, secretInteger, s_prime)) % s_prime;
        if (serverPublicInteger <= BigInteger.Zero)
        {
            return PasswordSrpVerificationResult.InvalidServerSecret;
        }

        byte[] serverPublicValue = PadInteger(serverPublicInteger);
        byte[] clientPublicPadded = PadInteger(clientPublicInteger);
        byte[] scramblingHash = Hash(clientPublicPadded, serverPublicValue);
        BigInteger scramblingParameter = ToInteger(scramblingHash);
        if (scramblingParameter.IsZero)
        {
            return PasswordSrpVerificationResult.InvalidClientPublicValue;
        }

        BigInteger sharedBase = clientPublicInteger * BigInteger.ModPow(
            verifierInteger, scramblingParameter, s_prime) % s_prime;
        if (sharedBase.IsZero)
        {
            return PasswordSrpVerificationResult.InvalidClientPublicValue;
        }

        BigInteger sharedSecretInteger = BigInteger.ModPow(
            sharedBase, secretInteger, s_prime);
        byte[] sharedSecret = PadInteger(sharedSecretInteger);
        byte[] sessionKey = Hash(sharedSecret);
        byte[] expectedProof = Hash(
            s_primeGeneratorHashXor,
            Hash(salt1),
            Hash(salt2),
            clientPublicPadded,
            serverPublicValue,
            sessionKey);

        bool matches = CryptographicOperations.FixedTimeEquals(
            expectedProof, clientProof);

        CryptographicOperations.ZeroMemory(sharedSecret);
        CryptographicOperations.ZeroMemory(sessionKey);
        CryptographicOperations.ZeroMemory(expectedProof);
        return matches
            ? PasswordSrpVerificationResult.Success
            : PasswordSrpVerificationResult.InvalidClientProof;
    }

    private static bool IsValidGroupElement(ReadOnlySpan<byte> value)
    {
        if (value.Length > PaddedLength)
        {
            return false;
        }

        BigInteger integer = ToInteger(value);
        return integer > BigInteger.Zero && integer < s_prime;
    }

    private static BigInteger ToInteger(ReadOnlySpan<byte> value) =>
        new(value, isUnsigned: true, isBigEndian: true);

    private static byte[] PadInteger(int value) =>
        PadInteger(new BigInteger(value));

    private static byte[] PadInteger(BigInteger value)
    {
        byte[] encoded = value.ToByteArray(isUnsigned: true,
            isBigEndian: true);
        if (encoded.Length > PaddedLength)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        byte[] padded = new byte[PaddedLength];
        encoded.CopyTo(padded.AsSpan(PaddedLength - encoded.Length));
        return padded;
    }

    private static byte[] Hash(ReadOnlySpan<byte> value) =>
        SHA256.HashData(value);

    private static byte[] Hash(ReadOnlySpan<byte> first,
        ReadOnlySpan<byte> second)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(first);
        hash.AppendData(second);
        return hash.GetHashAndReset();
    }

    private static byte[] Hash(
        ReadOnlySpan<byte> first,
        ReadOnlySpan<byte> second,
        ReadOnlySpan<byte> third,
        ReadOnlySpan<byte> fourth,
        ReadOnlySpan<byte> fifth,
        ReadOnlySpan<byte> sixth)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(first);
        hash.AppendData(second);
        hash.AppendData(third);
        hash.AppendData(fourth);
        hash.AppendData(fifth);
        hash.AppendData(sixth);
        return hash.GetHashAndReset();
    }

    private static byte[] Xor(ReadOnlySpan<byte> left,
        ReadOnlySpan<byte> right)
    {
        if (left.Length != right.Length)
        {
            throw new ArgumentException("Hash lengths must match.");
        }

        byte[] result = new byte[left.Length];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = (byte)(left[i] ^ right[i]);
        }

        return result;
    }
}
