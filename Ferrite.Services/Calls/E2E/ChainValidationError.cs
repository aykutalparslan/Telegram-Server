// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Calls.E2E;

// The reference's block-rejection taxonomy
// (TDLib tde2e/td/e2e/e2e_errors.h). Kept as a distinct enum from the
// wire error because the differential vectors compare against these names.
public enum ChainValidationError
{
    None = 0,
    InvalidBlock,
    NoChanges,
    InvalidSignature,
    HashMismatch,
    HeightMismatch,
    InvalidStateProofGroup,
    InvalidStateProofSecret,
    NoPermissions,
    InvalidGroupState,
    InvalidSharedSecret,
}

// The two sub-chains that exist. TDLib logs an error and drops anything else
// (GroupCallManager.cpp:5276).
public static class GroupCallChainIds
{
    public const int State = 0;
    public const int Broadcast = 1;

    public static bool IsValid(int subChainId) => subChainId is State or Broadcast;
}

public static class ChainValidationErrors
{
    public const string BlockInvalid = "BLOCK_INVALID";
    public const string BlockHeightMismatch = "BLOCK_HEIGHT_MISMATCH";
    public const string BlockHashMismatch = "BLOCK_HASH_MISMATCH";

    // Height and hash mismatches are the two cases a client recovers from by
    // refetching the head and rebuilding, so they stay distinguishable on the
    // wire. Everything else is a block the client should not retry unchanged.
    public static string ToWireError(ChainValidationError error) => error switch
    {
        ChainValidationError.HeightMismatch => BlockHeightMismatch,
        ChainValidationError.HashMismatch => BlockHashMismatch,
        _ => BlockInvalid,
    };
}
