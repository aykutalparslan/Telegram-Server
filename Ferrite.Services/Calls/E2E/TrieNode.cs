// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Security.Cryptography;

namespace Ferrite.Services.Calls.E2E;

public enum TrieNodeKind
{
    Empty = 0,
    Leaf = 1,
    Inner = 2,
    Pruned = 3,
}

public sealed class TrieNode
{
    private TrieNode()
    {
        Kind = TrieNodeKind.Empty;
        Hash = ComputeHash();
    }

    private TrieNode(TrieBitString keySuffix, byte[] value)
    {
        Kind = TrieNodeKind.Leaf;
        KeySuffix = keySuffix;
        Value = value;
        Hash = ComputeHash();
    }

    private TrieNode(TrieBitString prefix, TrieNode left, TrieNode right)
    {
        Kind = TrieNodeKind.Inner;
        Prefix = prefix;
        Left = left;
        Right = right;
        Hash = ComputeHash();
    }

    private TrieNode(byte[] hash, long prunedOffset, TrieBitString baseBitString)
    {
        Kind = TrieNodeKind.Pruned;
        Hash = hash;
        PrunedOffset = prunedOffset;
        BaseBitString = baseBitString;
    }

    public TrieNodeKind Kind { get; private set; }
    public byte[] Hash { get; private set; }
    public TrieBitString KeySuffix { get; private set; }
    public byte[] Value { get; private set; } = Array.Empty<byte>();
    public TrieBitString Prefix { get; private set; }
    public TrieNode? Left { get; private set; }
    public TrieNode? Right { get; private set; }
    public long PrunedOffset { get; private set; } = -1;
    public TrieBitString BaseBitString { get; private set; }

    public static TrieNode Empty { get; } = new TrieNode();

    public static TrieNode Leaf(TrieBitString keySuffix, byte[] value) =>
        new(keySuffix, value);

    public static TrieNode Inner(TrieBitString prefix, TrieNode left, TrieNode right) =>
        new(prefix, left, right);

    public static TrieNode Pruned(byte[] hash) =>
        new(hash, -1, default);

    public static TrieNode Pruned(byte[] hash, long offset, TrieBitString baseBitString) =>
        new(hash, offset, baseBitString);

    public void TryLoad(ReadOnlySpan<byte> snapshot)
    {
        if (Kind != TrieNodeKind.Pruned) return;
        if (PrunedOffset < 0)
        {
            throw new ChainCodecException("cannot load a pruned node");
        }
        if (PrunedOffset > snapshot.Length)
        {
            throw new ChainCodecException("cannot load a pruned node: invalid offset");
        }

        TrieBitString baseBitString = BaseBitString.HasData
            ? BaseBitString
            : TrieBitString.Allocate(BaseBitString.BeginBitInByte, BaseBitString.BitLength);

        var loaded = Trie.FetchNodeFromSnapshot(snapshot[(int)PrunedOffset..], baseBitString);
        if (!loaded.Hash.AsSpan().SequenceEqual(Hash))
        {
            throw new ChainCodecException("cannot load a pruned node: hash mismatch");
        }

        Kind = loaded.Kind;
        KeySuffix = loaded.KeySuffix;
        Value = loaded.Value;
        Prefix = loaded.Prefix;
        Left = loaded.Left;
        Right = loaded.Right;
        PrunedOffset = -1;
        BaseBitString = default;
        Hash = loaded.Hash;
    }

    private byte[] ComputeHash()
    {
        var writer = new TrieByteWriter();
        writer.WriteInt32((int)Kind);
        switch (Kind)
        {
            case TrieNodeKind.Leaf:
                KeySuffix.Store(writer);
                writer.WriteTlString(Value);
                break;
            case TrieNodeKind.Inner:
                Prefix.Store(writer);
                writer.WriteBytes(Left!.Hash);
                writer.WriteBytes(Right!.Hash);
                break;
            case TrieNodeKind.Empty:
                break;
            default:
                throw new ChainCodecException("cannot hash a pruned node");
        }
        return SHA256.HashData(writer.ToArray());
    }
}
