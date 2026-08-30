// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Calls.E2E;

public sealed class ChainKeyValueState
{
    private TrieNode _root;
    private readonly byte[] _snapshot;

    private ChainKeyValueState(TrieNode root, byte[] snapshot)
    {
        _root = root;
        _snapshot = snapshot;
    }

    public static ChainKeyValueState Empty() =>
        new(TrieNode.Empty, Array.Empty<byte>());

    public static ChainKeyValueState FromHash(byte[] hash) =>
        new(TrieNode.Pruned(hash), Array.Empty<byte>());

    public static ChainKeyValueState FromSnapshot(byte[] snapshot) =>
        new(Trie.ParseFromSnapshot(snapshot), snapshot);

    public byte[] Hash => _root.Hash;

    public ChainKeyValueState Clone() => new(_root, _snapshot);

    private static TrieBitString RequireExactKey(ReadOnlySpan<byte> key)
    {
        if (key.Length != 32) throw new ChainCodecException("Invalid key size");
        return TrieBitString.FromKey(key);
    }

    public void SetValue(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
    {
        _root = Trie.Set(_root, RequireExactKey(key), value, _snapshot);
    }

    public byte[] GetValue(ReadOnlySpan<byte> key) =>
        Trie.Get(_root, RequireExactKey(key), _snapshot);

    public byte[] GenerateProof(IReadOnlyList<byte[]> keys)
    {
        var bits = new List<TrieBitString>(keys.Count);
        foreach (var key in keys)
        {
            bits.Add(TrieBitString.FromKey(key));
        }
        return Trie.SerializeForNetwork(Trie.Prune(_root, bits, _snapshot));
    }

    public byte[] BuildSnapshot() => Trie.SerializeForSnapshot(_root, _snapshot);
}
