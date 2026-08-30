// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Crypto;
using Ferrite.TL.e2eChain.e2e;

namespace Ferrite.Services.Calls.E2E;

public static class ChainBroadcastValidator
{
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
            return ChainValidationError.InvalidBlock;
        }

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
