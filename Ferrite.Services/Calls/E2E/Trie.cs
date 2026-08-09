// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Calls.E2E;

// A direct port of TDLib tde2e/td/e2e/Trie.cpp. The client does not
// hold the full key-value state and needs a proof of every changed key to build
// its next block, so a divergence here produces blocks the client cannot build
// on with no local symptom.
public static class Trie
{
    public static TrieNode Set(TrieNode node, TrieBitString key, ReadOnlySpan<byte> value,
        ReadOnlySpan<byte> snapshot)
    {
        if (node.Kind == TrieNodeKind.Pruned)
        {
            node.TryLoad(snapshot);
        }

        switch (node.Kind)
        {
            case TrieNodeKind.Empty:
                return TrieNode.Leaf(key, value.ToArray());

            case TrieNodeKind.Leaf:
            {
                if (key.ValueEquals(node.KeySuffix))
                {
                    return TrieNode.Leaf(key, value.ToArray());
                }

                int i = key.CommonPrefixLength(node.KeySuffix);
                var commonPrefix = key.Substr(0, i);
                bool bit = key.GetBit(i) != 0;

                var left = TrieNode.Leaf(key.Substr(i + 1), value.ToArray());
                var right = TrieNode.Leaf(node.KeySuffix.Substr(i + 1), node.Value);
                if (bit)
                {
                    (left, right) = (right, left);
                }
                return TrieNode.Inner(commonPrefix, left, right);
            }

            case TrieNodeKind.Inner:
            {
                int i = node.Prefix.CommonPrefixLength(key);
                if (i < node.Prefix.BitLength)
                {
                    var commonPrefix = node.Prefix.Substr(0, i);
                    var remainingPrefix = node.Prefix.Substr(i + 1);
                    bool bit = node.Prefix.GetBit(i) != 0;

                    var left = TrieNode.Inner(remainingPrefix, node.Left!, node.Right!);
                    var right = TrieNode.Leaf(key.Substr(i + 1), value.ToArray());
                    if (bit)
                    {
                        (left, right) = (right, left);
                    }
                    return TrieNode.Inner(commonPrefix, left, right);
                }

                var newLeft = node.Left!;
                var newRight = node.Right!;
                if (key.GetBit(i) != 0)
                {
                    newRight = Set(newRight, key.Substr(i + 1), value, snapshot);
                }
                else
                {
                    newLeft = Set(newLeft, key.Substr(i + 1), value, snapshot);
                }
                return TrieNode.Inner(node.Prefix, newLeft, newRight);
            }

            default:
                throw new ChainCodecException("unreachable trie node kind");
        }
    }

    public static byte[] Get(TrieNode node, TrieBitString key, ReadOnlySpan<byte> snapshot)
    {
        if (node.Kind == TrieNodeKind.Pruned)
        {
            node.TryLoad(snapshot);
        }

        switch (node.Kind)
        {
            case TrieNodeKind.Empty:
                return Array.Empty<byte>();

            case TrieNodeKind.Leaf:
                return key.ValueEquals(node.KeySuffix) ? node.Value : Array.Empty<byte>();

            case TrieNodeKind.Inner:
            {
                int prefixLength = node.Prefix.BitLength;
                if (key.CommonPrefixLength(node.Prefix) != prefixLength)
                {
                    return Array.Empty<byte>();
                }
                return key.GetBit(prefixLength) != 0
                    ? Get(node.Right!, key.Substr(prefixLength + 1), snapshot)
                    : Get(node.Left!, key.Substr(prefixLength + 1), snapshot);
            }

            default:
                return Array.Empty<byte>();
        }
    }

    // prune_node from Trie.cpp:219-260. Two rules are easy to get wrong and
    // both change the proof bytes: a Leaf is returned whole even when the key
    // list is non-empty, and an Inner node with an empty key list collapses to
    // a Pruned stand-in.
    public static TrieNode Prune(TrieNode node, IReadOnlyList<TrieBitString> keys,
        ReadOnlySpan<byte> snapshot)
    {
        if (node.Kind == TrieNodeKind.Pruned)
        {
            node.TryLoad(snapshot);
        }

        if (node.Kind == TrieNodeKind.Empty)
        {
            return node;
        }
        if (keys.Count == 0)
        {
            return TrieNode.Pruned(node.Hash);
        }
        if (node.Kind == TrieNodeKind.Leaf)
        {
            return node;
        }
        if (node.Kind != TrieNodeKind.Inner)
        {
            return node;
        }

        var leftKeys = new List<TrieBitString>();
        var rightKeys = new List<TrieBitString>();
        int prefixLength = node.Prefix.BitLength;
        foreach (var key in keys)
        {
            if (key.CommonPrefixLength(node.Prefix) != prefixLength) continue;
            if (key.GetBit(prefixLength) != 0)
            {
                rightKeys.Add(key.Substr(prefixLength + 1));
            }
            else
            {
                leftKeys.Add(key.Substr(prefixLength + 1));
            }
        }

        var left = Prune(node.Left!, leftKeys, snapshot);
        var right = Prune(node.Right!, rightKeys, snapshot);
        return TrieNode.Inner(node.Prefix, left, right);
    }

    public static byte[] SerializeForNetwork(TrieNode node)
    {
        var writer = new TrieByteWriter();
        StoreForNetwork(node, writer);
        return writer.ToArray();
    }

    private static void StoreForNetwork(TrieNode node, TrieByteWriter writer)
    {
        writer.WriteInt32((int)node.Kind);
        switch (node.Kind)
        {
            case TrieNodeKind.Leaf:
                node.KeySuffix.Store(writer);
                writer.WriteTlString(node.Value);
                break;
            case TrieNodeKind.Inner:
                node.Prefix.Store(writer);
                StoreForNetwork(node.Left!, writer);
                StoreForNetwork(node.Right!, writer);
                break;
            case TrieNodeKind.Pruned:
                writer.WriteBytes(node.Hash);
                break;
            case TrieNodeKind.Empty:
                break;
            default:
                throw new ChainCodecException("unreachable trie node kind");
        }
    }

    public static TrieNode ParseFromNetwork(ReadOnlySpan<byte> data)
    {
        var reader = new TrieByteReader(data);
        var baseBitString = TrieBitString.Allocate(0, 256);
        var node = ParseFromNetwork(ref reader, baseBitString);
        if (!reader.AtEnd)
        {
            throw new ChainCodecException("trailing bytes in trie proof");
        }
        return node;
    }

    private static TrieNode ParseFromNetwork(ref TrieByteReader reader, TrieBitString baseBitString)
    {
        var kind = (TrieNodeKind)reader.ReadInt32();
        switch (kind)
        {
            case TrieNodeKind.Leaf:
            {
                var keySuffix = TrieBitString.Fetch(ref reader, baseBitString);
                byte[] value = reader.ReadTlString();
                return TrieNode.Leaf(keySuffix, value);
            }
            case TrieNodeKind.Inner:
            {
                var prefix = TrieBitString.Fetch(ref reader, baseBitString);
                var leftBase = baseBitString.Substr(prefix.BitLength + 1);
                var left = ParseFromNetwork(ref reader, leftBase);
                // The right sibling gets a FRESH buffer at the same within-byte
                // offset. Sharing the left's buffer would let the right's bits
                // overwrite the left's inside the byte they straddle.
                var rightBase = TrieBitString.Allocate(leftBase.BeginBitInByte, leftBase.BitLength);
                var right = ParseFromNetwork(ref reader, rightBase);
                return TrieNode.Inner(prefix, left, right);
            }
            case TrieNodeKind.Pruned:
                return TrieNode.Pruned(reader.ReadBytes(32).ToArray());
            case TrieNodeKind.Empty:
                return TrieNode.Empty;
            default:
                throw new ChainCodecException("unknown trie node kind");
        }
    }

    // store_for_snapshot from Trie.cpp:400-454: an 8-byte root-offset header
    // followed by a post-order layout, where each inner node records its
    // children's absolute offsets and hashes so a subtree can be loaded lazily.
    public static byte[] SerializeForSnapshot(TrieNode node, ReadOnlySpan<byte> snapshot)
    {
        var writer = new TrieByteWriter();
        writer.WriteInt64(0);
        long rootOffset = StoreForSnapshot(node, writer, snapshot);
        writer.WriteInt64At(0, rootOffset);
        return writer.ToArray();
    }

    private static long StoreForSnapshot(TrieNode node, TrieByteWriter writer,
        ReadOnlySpan<byte> snapshot)
    {
        if (node.Kind == TrieNodeKind.Pruned)
        {
            node.TryLoad(snapshot);
        }

        switch (node.Kind)
        {
            case TrieNodeKind.Leaf:
            {
                long offset = writer.Position;
                writer.WriteInt32((int)node.Kind);
                node.KeySuffix.Store(writer);
                writer.WriteTlString(node.Value);
                return offset;
            }
            case TrieNodeKind.Inner:
            {
                long leftOffset = StoreForSnapshot(node.Left!, writer, snapshot);
                long rightOffset = StoreForSnapshot(node.Right!, writer, snapshot);
                long offset = writer.Position;
                writer.WriteInt32((int)node.Kind);
                node.Prefix.Store(writer);
                writer.WriteInt64(leftOffset);
                writer.WriteBytes(node.Left!.Hash);
                writer.WriteInt64(rightOffset);
                writer.WriteBytes(node.Right!.Hash);
                return offset;
            }
            case TrieNodeKind.Empty:
            {
                long offset = writer.Position;
                writer.WriteInt32((int)node.Kind);
                return offset;
            }
            default:
                throw new ChainCodecException("unreachable trie node kind");
        }
    }

    public static TrieNode ParseFromSnapshot(ReadOnlySpan<byte> snapshot)
    {
        var reader = new TrieByteReader(snapshot);
        long rootOffset = reader.ReadInt64();
        if (rootOffset < 0 || rootOffset >= snapshot.Length)
        {
            throw new ChainCodecException("invalid snapshot root offset");
        }
        return FetchNodeFromSnapshot(snapshot[(int)rootOffset..], TrieBitString.Allocate(0, 256));
    }

    internal static TrieNode FetchNodeFromSnapshot(ReadOnlySpan<byte> slice,
        TrieBitString baseBitString)
    {
        var reader = new TrieByteReader(slice);
        var kind = (TrieNodeKind)reader.ReadInt32();
        switch (kind)
        {
            case TrieNodeKind.Leaf:
            {
                var keySuffix = TrieBitString.Fetch(ref reader, baseBitString);
                byte[] value = reader.ReadTlString();
                return TrieNode.Leaf(keySuffix, value);
            }
            case TrieNodeKind.Inner:
            {
                var prefix = TrieBitString.Fetch(ref reader, baseBitString);
                long leftOffset = reader.ReadInt64();
                byte[] leftHash = reader.ReadBytes(32).ToArray();
                long rightOffset = reader.ReadInt64();
                byte[] rightHash = reader.ReadBytes(32).ToArray();

                var leftBase = baseBitString.Substr(prefix.BitLength + 1);
                // The right child's base is recorded WITHOUT a buffer; TryLoad
                // allocates one, which keeps siblings from sharing bytes.
                var rightBase = new TrieBitString(null, leftBase.BeginBitInByte, leftBase.BitLength);

                var left = TrieNode.Pruned(leftHash, leftOffset, leftBase);
                var right = TrieNode.Pruned(rightHash, rightOffset, rightBase);
                return TrieNode.Inner(prefix, left, right);
            }
            case TrieNodeKind.Empty:
                return TrieNode.Empty;
            default:
                throw new ChainCodecException("failed to parse trie node");
        }
    }
}
