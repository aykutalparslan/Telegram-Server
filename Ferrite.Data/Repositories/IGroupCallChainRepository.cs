// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public interface IGroupCallChainRepository
{
    ValueTask<TLDto.TLGroupCallChainState?> GetChainStateAsync(long callId, int subChainId,
        CancellationToken cancellationToken = default);
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
