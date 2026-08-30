// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Calls.E2E;

public readonly record struct GroupCallChainAppend(ChainValidationError Error, int Height,
    int NextOffset);

public readonly record struct GroupCallChainWindow(IReadOnlyList<byte[]> Blocks,
    int NextOffset);

public interface IGroupCallChainService
{
    ValueTask<GroupCallChainAppend> TryAppendAsync(long callId, int subChainId, long userId,
        byte[] serializedBlock, CancellationToken cancellationToken = default);
    ValueTask<GroupCallChainWindow> GetWindowAsync(long callId, int subChainId, int offset,
        int limit, CancellationToken cancellationToken = default);
    ValueTask<ChainGroupStateValue?> GetGroupStateAsync(long callId,
        CancellationToken cancellationToken = default);
    ValueTask DiscardAsync(long callId, CancellationToken cancellationToken = default);
}
