// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Buffers.Binary;
using System.Security.Cryptography;
using Ferrite.TL;
using Ferrite.TL.e2eChain.e2e;

namespace Ferrite.Services.Calls.E2E;

public static class ChainBlockCodec
{
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
            error = ChainValidationError.InvalidBlock;
            return false;
        }
    }

    public static byte[] SerializeWithZeroSignature(ChainBlockValue block)
    {
        var buffer = (byte[])block.Raw.Clone();
        buffer.AsSpan(SignatureOffset, SignatureLength).Clear();
        return buffer;
    }

    public static byte[] SerializeWithZeroSignature(ReadOnlySpan<byte> broadcast)
    {
        var buffer = broadcast.ToArray();
        buffer.AsSpan(SignatureOffset, SignatureLength).Clear();
        return buffer;
    }

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

    private static bool TryReadGroupState(Span<byte> span,
        out ChainGroupStateValue groupState) =>
        ChainValueCodec.TryReadGroupState(span, out groupState);

    private static bool TryReadSharedKey(Span<byte> span, out ChainSharedKeyValue sharedKey) =>
        ChainValueCodec.TryReadSharedKey(span, out sharedKey);
}
