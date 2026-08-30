// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Calls.E2E;

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

    public static string ToWireError(ChainValidationError error) => error switch
    {
        ChainValidationError.HeightMismatch => BlockHeightMismatch,
        ChainValidationError.HashMismatch => BlockHashMismatch,
        _ => BlockInvalid,
    };
}
