// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Buffers.Binary;
using System.Security.Cryptography;
using Ferrite.TL;
using Ferrite.TL.e2eChain.e2e;

namespace Ferrite.Services.Calls.E2E;

// Reads a client-supplied block into plain records the validator can hold
// across awaits, and derives the two byte strings the protocol depends on.
public static class ChainBlockCodec
{
    // e2e.chainBlock lays out `signature:int512` immediately after the 4-byte
    // constructor id, so the signature window is always Raw[4..68]. The
    // generated GetOffset agrees; this is asserted by a schema test.
    private const int SignatureOffset = 4;
    private const int SignatureLength = 64;
    private const int MinimumBlockLength = SignatureOffset + SignatureLength;

    public static bool TryParse(ReadOnlySpan<byte> serialized, out ChainBlockValue block,
        out ChainValidationError error)
    {
        block = null!;
        if (serialized.Length < MinimumBlockLength)
        {
            error = ChainValidationError.InvalidBlock;
            return false;
        }

        try
        {
            var span = serialized.ToArray().AsSpan();
            var view = new ChainBlockView(span);
            if (view.Constructor != Constructors.e2eChain_ChainBlock ||
                !view.Is(out ChainBlock parsed))
            {
                error = ChainValidationError.InvalidBlock;
                return false;
            }

            var changes = new List<ChainChangeValue>();
            var changeVector = parsed.Changes;
            for (int i = 0; i < changeVector.Count; i++)
            {
                if (!TryReadChange(changeVector.ReadTLObject(), out var change))
                {
                    error = ChainValidationError.InvalidBlock;
                    return false;
                }
                changes.Add(change);
            }

            if (!TryReadStateProof(parsed.StateProof, out var stateProof))
            {
                error = ChainValidationError.InvalidBlock;
                return false;
            }

            byte[] raw = serialized.ToArray();
            block = new ChainBlockValue
            {
                Raw = raw,
                // Block::calc_hash: sha256 of the serialization AS RECEIVED,
                // signature included. A height of -1 hashes to zero, but that
                // is the synthetic genesis block and never arrives on the wire.
                Hash = SHA256.HashData(raw),
                Signature = parsed.Signature.ToArray(),
                PrevBlockHash = parsed.PrevBlockHash.ToArray(),
                Changes = changes,
                Height = parsed.Height,
                StateProof = stateProof,
                SignaturePublicKey = parsed.Flags[0] ? parsed.SignaturePublicKey.ToArray() : null,
            };
            error = ChainValidationError.None;
            return true;
        }
        catch (Exception)
        {
            // Any malformed length, unknown nested constructor or truncated
            // vector is a rejected block, not a server fault.
            error = ChainValidationError.InvalidBlock;
            return false;
        }
    }

    // The reference signs `serialize_boxed(block)` with the signature field
    // zeroed (utils.h:117-134). Zeroing the window inside Raw reproduces that
    // without rebuilding the block, which is the point: a rebuild could differ
    // in padding and would verify against nothing.
    public static byte[] SerializeWithZeroSignature(ChainBlockValue block)
    {
        var buffer = (byte[])block.Raw.Clone();
        buffer.AsSpan(SignatureOffset, SignatureLength).Clear();
        return buffer;
    }

    // Same layout rule for the two sub-chain 1 broadcast constructors.
    public static byte[] SerializeWithZeroSignature(ReadOnlySpan<byte> broadcast)
    {
        var buffer = broadcast.ToArray();
        buffer.AsSpan(SignatureOffset, SignatureLength).Clear();
        return buffer;
    }

    /// <summary>
    /// A stored block in the form the server must hand back. tde2e distinguishes
    /// a block a client built from a block a server served by the leading
    /// constructor id alone: the server form is the real id PLUS ONE
    /// (Blockchain::from_local_to_server). A client refuses to apply a block
    /// carrying an unincremented id with "Trying to apply local block, not from
    /// server", which surfaces only as a call that silently never becomes
    /// encrypted. It applies to both sub-chains, and the stored bytes must stay
    /// in the received form because that is what the hash and signature cover.
    ///
    /// The incremented value is deliberately NOT a constructor id any schema
    /// knows, so no generated type can express it; like the trie, this framing
    /// marker is tde2e's own, not TL.
    /// </summary>
    public static byte[] ToServerForm(ReadOnlySpan<byte> localBlock)
    {
        var buffer = localBlock.ToArray();
        if (buffer.Length >= sizeof(int))
        {
            int magic = BinaryPrimitives.ReadInt32LittleEndian(buffer);
            BinaryPrimitives.WriteInt32LittleEndian(buffer, magic + 1);
        }
        return buffer;
    }

    private static bool TryReadChange(Span<byte> span, out ChainChangeValue change)
    {
        change = null!;
        var view = new ChainChangeView(span);
        if (view.Is(out ChainChangeNoop noop))
        {
            change = new ChainChangeNoopValue(noop.Nonce.ToArray());
            return true;
        }
        if (view.Is(out ChainChangeSetValue setValue))
        {
            change = new ChainChangeSetValueValue(setValue.Key.ToArray(), setValue.Value.ToArray());
            return true;
        }
        if (view.Is(out ChainChangeSetGroupState setGroupState))
        {
            if (!TryReadGroupState(setGroupState.GroupState, out var groupState)) return false;
            change = new ChainChangeSetGroupStateValue(groupState);
            return true;
        }
        if (view.Is(out ChainChangeSetSharedKey setSharedKey))
        {
            if (!TryReadSharedKey(setSharedKey.SharedKey, out var sharedKey)) return false;
            change = new ChainChangeSetSharedKeyValue(sharedKey);
            return true;
        }
        return false;
    }

    private static bool TryReadStateProof(Span<byte> span, out ChainStateProofValue stateProof)
    {
        stateProof = null!;
        var view = new ChainStateProofView(span);
        if (!view.Is(out ChainStateProof proof)) return false;

        ChainGroupStateValue? groupState = null;
        if (proof.Flags[0])
        {
            if (!TryReadGroupState(proof.GroupState, out var value)) return false;
            groupState = value;
        }

        ChainSharedKeyValue? sharedKey = null;
        if (proof.Flags[1])
        {
            if (!TryReadSharedKey(proof.SharedKey, out var value)) return false;
            sharedKey = value;
        }

        stateProof = new ChainStateProofValue(proof.KvHash.ToArray(), groupState, sharedKey);
        return true;
    }

    // The persisted head reads these back with the same reader, so a block and a
    // restored chain can never disagree about what a group state means.
    private static bool TryReadGroupState(Span<byte> span,
        out ChainGroupStateValue groupState) =>
        ChainValueCodec.TryReadGroupState(span, out groupState);

    private static bool TryReadSharedKey(Span<byte> span, out ChainSharedKeyValue sharedKey) =>
        ChainValueCodec.TryReadSharedKey(span, out sharedKey);
}
