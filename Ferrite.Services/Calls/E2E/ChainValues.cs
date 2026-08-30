// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Calls.E2E;

public static class ChainPermissionFlags
{
    public const int AddUsers = 1 << 0;
    public const int RemoveUsers = 1 << 1;
    public const int SetValue = 1 << 2;
    public const int AllPermissions = (1 << 3) - 1;
    public const int IsParticipant = 1 << 30;
}

public readonly record struct ChainPermissions(int Flags)
{
    public bool MayAddUsers => (Flags & ChainPermissionFlags.AddUsers) != 0;
    public bool MayRemoveUsers => (Flags & ChainPermissionFlags.RemoveUsers) != 0;
    public bool MaySetValue => (Flags & ChainPermissionFlags.SetValue) != 0;
    public bool IsParticipant => (Flags & ChainPermissionFlags.IsParticipant) != 0;

    public bool MayChangeSharedKey => IsParticipant && (MayRemoveUsers || MayAddUsers);
}

public sealed record ChainParticipant(long UserId, byte[] PublicKey, int Flags, int Version)
{
    public bool Equals(ChainParticipant? other) =>
        other is not null && UserId == other.UserId && Flags == other.Flags &&
        Version == other.Version && PublicKey.AsSpan().SequenceEqual(other.PublicKey);

    public override int GetHashCode() => HashCode.Combine(UserId, Flags, Version);
}

public sealed record ChainGroupStateValue(
    IReadOnlyList<ChainParticipant> Participants, int ExternalPermissions)
{
    public static ChainGroupStateValue Empty { get; } =
        new(Array.Empty<ChainParticipant>(), 0);

    public ChainParticipant? FindByPublicKey(ReadOnlySpan<byte> publicKey)
    {
        foreach (var participant in Participants)
        {
            if (participant.PublicKey.AsSpan().SequenceEqual(publicKey)) return participant;
        }
        return null;
    }

    public ChainParticipant? FindByUserId(long userId)
    {
        foreach (var participant in Participants)
        {
            if (participant.UserId == userId) return participant;
        }
        return null;
    }

    public ChainPermissions GetPermissions(ReadOnlySpan<byte> publicKey, int limitPermissions)
    {
        limitPermissions &= ChainPermissionFlags.AllPermissions;
        var participant = FindByPublicKey(publicKey);
        if (participant != null)
        {
            return new ChainPermissions(
                (participant.Flags & limitPermissions) | ChainPermissionFlags.IsParticipant);
        }
        return new ChainPermissions(ExternalPermissions & limitPermissions);
    }

    public bool Equals(ChainGroupStateValue? other)
    {
        if (other is null) return false;
        if (ExternalPermissions != other.ExternalPermissions) return false;
        if (Participants.Count != other.Participants.Count) return false;
        for (int i = 0; i < Participants.Count; i++)
        {
            if (!Participants[i].Equals(other.Participants[i])) return false;
        }
        return true;
    }

    public override int GetHashCode() => HashCode.Combine(ExternalPermissions, Participants.Count);
}

public sealed record ChainSharedKeyValue(
    byte[] Ek, byte[] EncryptedSharedKey,
    IReadOnlyList<long> DestUserId, IReadOnlyList<byte[]> DestHeader)
{
    public static ChainSharedKeyValue EmptyKey { get; } = new(
        new byte[32], Array.Empty<byte>(), Array.Empty<long>(), Array.Empty<byte[]>());

    public bool IsEmpty => Equals(EmptyKey);

    public bool Equals(ChainSharedKeyValue? other)
    {
        if (other is null) return false;
        if (!Ek.AsSpan().SequenceEqual(other.Ek)) return false;
        if (!EncryptedSharedKey.AsSpan().SequenceEqual(other.EncryptedSharedKey)) return false;
        if (DestUserId.Count != other.DestUserId.Count) return false;
        for (int i = 0; i < DestUserId.Count; i++)
        {
            if (DestUserId[i] != other.DestUserId[i]) return false;
        }
        if (DestHeader.Count != other.DestHeader.Count) return false;
        for (int i = 0; i < DestHeader.Count; i++)
        {
            if (!DestHeader[i].AsSpan().SequenceEqual(other.DestHeader[i])) return false;
        }
        return true;
    }

    public override int GetHashCode() => HashCode.Combine(DestUserId.Count, DestHeader.Count);
}

public abstract record ChainChangeValue;

public sealed record ChainChangeNoopValue(byte[] Nonce) : ChainChangeValue;

public sealed record ChainChangeSetValueValue(byte[] Key, byte[] Value) : ChainChangeValue;

public sealed record ChainChangeSetGroupStateValue(ChainGroupStateValue GroupState)
    : ChainChangeValue;

public sealed record ChainChangeSetSharedKeyValue(ChainSharedKeyValue SharedKey)
    : ChainChangeValue;

public sealed record ChainStateProofValue(
    byte[] KvHash, ChainGroupStateValue? GroupState, ChainSharedKeyValue? SharedKey);

public sealed class ChainBlockValue
{
    public required byte[] Raw { get; init; }
    public required byte[] Hash { get; init; }
    public required byte[] Signature { get; init; }
    public required byte[] PrevBlockHash { get; init; }
    public required IReadOnlyList<ChainChangeValue> Changes { get; init; }
    public required int Height { get; init; }
    public required ChainStateProofValue StateProof { get; init; }
    public byte[]? SignaturePublicKey { get; init; }
}
