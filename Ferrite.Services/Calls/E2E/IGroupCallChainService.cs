// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Calls.E2E;

/// <summary>One append attempt's outcome, as the handlers need to report it.</summary>
public readonly record struct GroupCallChainAppend(ChainValidationError Error, int Height,
    int NextOffset);

/// <summary>
/// One contiguous poll window. <see cref="NextOffset"/> is what the client stores
/// as its own next offset, so it must always equal the offset just past the last
/// returned block.
/// </summary>
public readonly record struct GroupCallChainWindow(IReadOnlyList<byte[]> Blocks,
    int NextOffset);

/// <summary>
/// The authoritative validator and ordering server for one conference call's
/// tde2e chain. Validation happens against the persisted head and publishes
/// nothing until the append commits, which is what keeps two clients that built
/// on the same height from forking.
/// </summary>
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
