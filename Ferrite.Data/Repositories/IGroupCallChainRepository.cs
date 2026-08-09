// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

/// <summary>
/// One conference call's two tde2e sub-chains. Sub-chain 0 is the validated
/// block chain; sub-chain 1 carries the opaque commit/reveal broadcasts. Both
/// are append-only sequences whose height IS the offset clients poll with.
/// </summary>
public interface IGroupCallChainRepository
{
    ValueTask<TLDto.TLGroupCallChainState?> GetChainStateAsync(long callId, int subChainId,
        CancellationToken cancellationToken = default);
    /// <summary>
    /// Appends one block at <paramref name="expectedHeight"/> + 1, committing the
    /// new head state with it. Returns <c>Committed == false</c> when another
    /// writer already took that height.
    /// </summary>
    ValueTask<GroupCallChainAppendResult> TryAppendBlockAsync(
        TLDto.TLGroupCallChainState newState, TLDto.TLGroupCallChainBlock block,
        int expectedHeight, CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<TLDto.TLGroupCallChainBlock>> GetBlocksAsync(long callId,
        int subChainId, int offset, int limit, CancellationToken cancellationToken = default);
    ValueTask<TLDto.TLGroupCallChainBlock?> GetLastBlockAsync(long callId, int subChainId,
        CancellationToken cancellationToken = default);
    ValueTask<int> GetNextOffsetAsync(long callId, int subChainId,
        CancellationToken cancellationToken = default);
    ValueTask DeleteChainAsync(long callId, CancellationToken cancellationToken = default);
}
