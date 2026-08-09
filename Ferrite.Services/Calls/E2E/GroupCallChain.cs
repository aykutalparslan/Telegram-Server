// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Calls.E2E;

// One conference call's sub-chain 0. The whole reason the server participates
// in this protocol is fork prevention: exactly one block wins at each height,
// so a block is validated against a clone and published only if the entire
// block succeeds.
public sealed class GroupCallChain
{
    private ChainState _state;

    private GroupCallChain(int height, byte[] headHash, ChainState state)
    {
        Height = height;
        HeadHash = headHash;
        _state = state;
    }

    // The synthetic genesis block sits at height -1 with an all-zero hash.
    public static GroupCallChain CreateEmpty() =>
        new(-1, new byte[32], ChainState.CreateEmpty());

    public static GroupCallChain Restore(int height, byte[] headHash, byte[] kvSnapshot,
        ChainGroupStateValue groupState, ChainSharedKeyValue sharedKey)
    {
        var keyValueState = kvSnapshot.Length == 0
            ? ChainKeyValueState.Empty()
            : ChainKeyValueState.FromSnapshot(kvSnapshot);
        return new GroupCallChain(height, headHash,
            new ChainState(keyValueState, groupState, sharedKey));
    }

    public int Height { get; private set; }
    public byte[] HeadHash { get; private set; }
    public byte[] KeyValueHash => _state.KeyValueState.Hash;
    public ChainGroupStateValue GroupState => _state.GroupState;
    public ChainSharedKeyValue SharedKey => _state.SharedKey;

    public ChainValidationError TryApplyBlock(ChainBlockValue block)
    {
        if (Height == int.MaxValue || block.Height != Height + 1)
        {
            return ChainValidationError.HeightMismatch;
        }
        if (!block.PrevBlockHash.AsSpan().SequenceEqual(HeadHash))
        {
            return ChainValidationError.HashMismatch;
        }

        var candidate = _state.Clone();
        var error = candidate.Apply(block);
        if (error != ChainValidationError.None) return error;

        // No failure path past this point: a rejected block must leave the
        // chain byte-identical so the client's retry against the real head is
        // safe.
        _state = candidate;
        HeadHash = block.Hash;
        Height = block.Height;
        return ChainValidationError.None;
    }

    public byte[] BuildSnapshot() => _state.KeyValueState.BuildSnapshot();

    public byte[] GenerateProof(IReadOnlyList<byte[]> keys) =>
        _state.KeyValueState.GenerateProof(keys);
}
