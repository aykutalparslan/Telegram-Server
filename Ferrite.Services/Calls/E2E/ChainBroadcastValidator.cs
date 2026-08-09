// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Crypto;
using Ferrite.TL.e2eChain.e2e;

namespace Ferrite.Services.Calls.E2E;

// Sub-chain 1 carries the commit-reveal nonce exchange behind the verification
// emojis. Its SEMANTICS -- phase ordering, sha256(nonce) matching its commit,
// the derived emoji hash -- are enforced client-side in Call.cpp:150-252, and
// every client re-validates every broadcast it receives. The server's job is
// authorship and ordering, so it checks the constructor, the claimed author,
// participation and the signature, then appends in arrival order.
//
// A chain_height ahead of the current sub-chain 0 head is deliberately NOT
// rejected: the client buffers future-height broadcasts (Call.cpp:117-126) and
// rejecting them here would break a rekey that races a join.
public static class ChainBroadcastValidator
{
    // Both constructors put the 64-byte signature immediately after the id,
    // exactly like a block, so the signed preimage is the payload with that
    // window zeroed.
    private const int SignatureOffset = 4;
    private const int SignatureLength = 64;

    public static ChainValidationError Validate(ReadOnlySpan<byte> broadcast,
        long callerUserId, ChainGroupStateValue groupState)
    {
        if (broadcast.Length < SignatureOffset + SignatureLength)
        {
            return ChainValidationError.InvalidBlock;
        }

        long authorUserId;
        try
        {
            var span = broadcast.ToArray().AsSpan();
            var view = new ChainGroupBroadcastView(span);
            // Both constructors are fixed size, so the payload must be exactly
            // one of them: the signature covers these bytes and clients re-parse
            // the row verbatim, so neither a truncation nor a trailing tail may
            // be stored as a broadcast.
            if (view.Is(out ChainGroupBroadcastNonceCommit commit))
            {
                if (ChainGroupBroadcastNonceCommit.ReadSize(span, 0) != broadcast.Length)
                {
                    return ChainValidationError.InvalidBlock;
                }
                authorUserId = commit.UserId;
            }
            else if (view.Is(out ChainGroupBroadcastNonceReveal reveal))
            {
                if (ChainGroupBroadcastNonceReveal.ReadSize(span, 0) != broadcast.Length)
                {
                    return ChainValidationError.InvalidBlock;
                }
                authorUserId = reveal.UserId;
            }
            else
            {
                return ChainValidationError.InvalidBlock;
            }
        }
        catch (Exception)
        {
            // A truncated or malformed payload is a rejected broadcast, not a
            // server fault, exactly as it is for a block.
            return ChainValidationError.InvalidBlock;
        }

        // A client may not broadcast as somebody else.
        if (authorUserId != callerUserId) return ChainValidationError.InvalidBlock;

        var participant = groupState.FindByUserId(callerUserId);
        if (participant == null) return ChainValidationError.NoPermissions;

        var preimage = broadcast.ToArray();
        preimage.AsSpan(SignatureOffset, SignatureLength).Clear();

        return Ed25519Verifier.Verify(participant.PublicKey, preimage,
            broadcast.Slice(SignatureOffset, SignatureLength))
            ? ChainValidationError.None
            : ChainValidationError.InvalidSignature;
    }
}
