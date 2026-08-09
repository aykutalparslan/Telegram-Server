// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.Utils;
using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Calls.E2E;

public sealed class GroupCallChainService : IGroupCallChainService
{
    private readonly IGroupCallChainRepository _groupCallChainRepository;

    // BLOCK_POLL_COUNT in pinned TDLib (GroupCallManager.h:205). A client never
    // asks for more, and a window larger than the client's own poll size would
    // just be discarded.
    public const int MaxWindow = 100;
    private const int MaxBroadcastAppendAttempts = 16;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IMTProtoTime _time;
    private readonly ILogger _log;

    public GroupCallChainService(IUnitOfWork unitOfWork, IGroupCallChainRepository groupCallChainRepository, IMTProtoTime time, ILogger log)
    {
        _groupCallChainRepository = groupCallChainRepository;

        _unitOfWork = unitOfWork;
        _time = time;
        _log = log;
    }

    private IGroupCallChainRepository Repository => _groupCallChainRepository;

    // Validate against the persisted head, then append under the repository's
    // per-(call, sub-chain) gate with the height we validated against. Two
    // clients that both built on height N have both validated successfully; the
    // repository decides which one is real, and the loser gets a height mismatch
    // so it refetches the head and rebuilds. This is the fork prevention the
    // protocol depends on, and it is why validation may not publish state before
    // the append commits.
    public async ValueTask<GroupCallChainAppend> TryAppendAsync(long callId, int subChainId,
        long userId, byte[] serializedBlock, CancellationToken cancellationToken = default)
    {
        if (subChainId is not (GroupCallSubChain.Blocks or GroupCallSubChain.Broadcast))
        {
            return new GroupCallChainAppend(ChainValidationError.InvalidBlock, -1, 0);
        }

        if (subChainId == GroupCallSubChain.Broadcast)
        {
            return await AppendBroadcastAsync(callId, userId, serializedBlock,
                cancellationToken);
        }

        GroupCallChain chain;
        int expectedHeight;
        using (TLDto.TLGroupCallChainState? stored = await Repository.GetChainStateAsync(
                   callId, subChainId, cancellationToken))
        {
            chain = stored == null
                ? GroupCallChain.CreateEmpty()
                : RestoreChain(stored.Value);
            expectedHeight = chain.Height;
        }

        if (!ChainBlockCodec.TryParse(serializedBlock, out var block, out var parseError))
        {
            return Rejected(parseError, expectedHeight);
        }

        var error = chain.TryApplyBlock(block);
        if (error != ChainValidationError.None)
        {
            return Rejected(error, expectedHeight);
        }

        GroupCallChainAppendResult committed;
        using (TLDto.TLGroupCallChainState state = BuildStateRow(callId, subChainId, chain))
        using (TLDto.TLGroupCallChainBlock row = BuildBlockRow(callId, subChainId,
                   block.Height, block.Raw, block.Hash))
        {
            committed = await Repository.TryAppendBlockAsync(state, row, expectedHeight,
                cancellationToken);
        }

        if (!committed.Committed)
        {
            _log.Debug($"🔗 chain append for call:{callId} sub:{subChainId} lost " +
                       $"height {expectedHeight + 1} to a concurrent writer");
            return Rejected(ChainValidationError.HeightMismatch, committed.Height);
        }

        await _unitOfWork.SaveAsync();
        return new GroupCallChainAppend(ChainValidationError.None, committed.Height,
            committed.Height + 1);
    }

    // Sub-chain 1 rows are opaque: a broadcast is not a chained block, so it has
    // no predecessor hash and is appended in arrival order at the next offset.
    private async ValueTask<GroupCallChainAppend> AppendBroadcastAsync(long callId,
        long userId, byte[] payload, CancellationToken cancellationToken)
    {
        ChainGroupStateValue groupState =
            await GetGroupStateAsync(callId, cancellationToken) ??
            ChainGroupStateValue.Empty;
        var error = ChainBroadcastValidator.Validate(payload, userId, groupState);

        if (error != ChainValidationError.None)
        {
            int currentHeight = await Repository.GetNextOffsetAsync(callId,
                GroupCallSubChain.Broadcast, cancellationToken) - 1;
            return Rejected(error, currentHeight);
        }

        // TDLib does not retry a rejected opaque broadcast. Unlike sub-chain 0,
        // there is no predecessor to rebuild and therefore no protocol reason to
        // expose an ordinary offset race to the client. Re-read the append-only
        // head and place this payload at the next free offset instead.
        GroupCallChainAppendResult committed = default;
        for (int attempt = 0; attempt < MaxBroadcastAppendAttempts; attempt++)
        {
            int expectedHeight = await Repository.GetNextOffsetAsync(callId,
                GroupCallSubChain.Broadcast, cancellationToken) - 1;
            int height = expectedHeight + 1;
            using TLDto.TLGroupCallChainState state = TLDto.GroupCallChainState.Builder()
                .CallId(callId)
                .SubChainId(GroupCallSubChain.Broadcast)
                .Height(height)
                .HeadHash(new byte[32])
                .KvSnapshot(Array.Empty<byte>())
                .Build();
            using TLDto.TLGroupCallChainBlock row = BuildBlockRow(callId,
                GroupCallSubChain.Broadcast, height, payload, new byte[32]);

            committed = await Repository.TryAppendBlockAsync(state, row, expectedHeight,
                cancellationToken);
            if (committed.Committed)
            {
                await _unitOfWork.SaveAsync();
                return new GroupCallChainAppend(ChainValidationError.None, committed.Height,
                    committed.Height + 1);
            }
        }

        _log.Debug($"🔗 broadcast append for call:{callId} exhausted " +
                   $"{MaxBroadcastAppendAttempts} concurrent offset retries");
        return Rejected(ChainValidationError.HeightMismatch, committed.Height);
    }

    public async ValueTask<GroupCallChainWindow> GetWindowAsync(long callId, int subChainId,
        int offset, int limit, CancellationToken cancellationToken = default)
    {
        // offset -1 is the client's opening question, "what is the head": the
        // reply is the head block alone, or nothing at all when the chain has
        // not started and the client must build a zero block.
        if (offset < 0)
        {
            using TLDto.TLGroupCallChainBlock? head = await Repository.GetLastBlockAsync(
                callId, subChainId, cancellationToken);
            int nextOffset = await Repository.GetNextOffsetAsync(callId, subChainId,
                cancellationToken);
            return new GroupCallChainWindow(
                head == null
                    ? Array.Empty<byte[]>()
                    : new[] { head.Value.AsGroupCallChainBlock().Block.ToArray() },
                nextOffset);
        }

        IReadOnlyList<TLDto.TLGroupCallChainBlock> window = await Repository.GetBlocksAsync(
            callId, subChainId, offset, Math.Min(limit, MaxWindow), cancellationToken);
        var blocks = new List<byte[]>(window.Count);
        foreach (TLDto.TLGroupCallChainBlock block in window)
        {
            using (block)
            {
                blocks.Add(block.AsGroupCallChainBlock().Block.ToArray());
            }
        }

        // TDLib applies the LAST (next_offset - its own next offset) entries, so
        // the window must be contiguous and end exactly at next_offset.
        return new GroupCallChainWindow(blocks, offset + blocks.Count);
    }

    public async ValueTask<ChainGroupStateValue?> GetGroupStateAsync(long callId,
        CancellationToken cancellationToken = default)
    {
        using TLDto.TLGroupCallChainState? stored = await Repository.GetChainStateAsync(
            callId, GroupCallSubChain.Blocks, cancellationToken);
        if (stored == null)
        {
            return null;
        }

        var view = stored.Value.AsGroupCallChainState();
        if (!view.Flags[0])
        {
            return ChainGroupStateValue.Empty;
        }
        return ChainValueCodec.TryReadGroupState(view.GroupState.ToArray(),
            out var groupState)
            ? groupState
            : null;
    }

    public ValueTask DiscardAsync(long callId, CancellationToken cancellationToken = default) =>
        Repository.DeleteChainAsync(callId, cancellationToken);

    private static GroupCallChainAppend Rejected(ChainValidationError error, int height) =>
        new(error, height, height + 1);

    // The head is restored from its snapshot rather than by replaying blocks:
    // the trie snapshot is the reference's own layout and reproduces the exact
    // kv hash the next block's state proof is checked against.
    private static GroupCallChain RestoreChain(TLDto.TLGroupCallChainState stored)
    {
        var view = stored.AsGroupCallChainState();
        var groupState = ChainGroupStateValue.Empty;
        if (view.Flags[0] &&
            ChainValueCodec.TryReadGroupState(view.GroupState.ToArray(), out var parsedState))
        {
            groupState = parsedState;
        }
        var sharedKey = ChainSharedKeyValue.EmptyKey;
        if (view.Flags[1] &&
            ChainValueCodec.TryReadSharedKey(view.SharedKey.ToArray(), out var parsedKey))
        {
            sharedKey = parsedKey;
        }

        return GroupCallChain.Restore(view.Height, view.HeadHash.ToArray(),
            view.KvSnapshot.ToArray(), groupState, sharedKey);
    }

    private static TLDto.TLGroupCallChainState BuildStateRow(long callId, int subChainId,
        GroupCallChain chain)
    {
        byte[] groupState = ChainValueCodec.SerializeGroupState(chain.GroupState);
        byte[]? sharedKey = chain.SharedKey.IsEmpty
            ? null
            : ChainValueCodec.SerializeSharedKey(chain.SharedKey);

        var builder = TLDto.GroupCallChainState.Builder()
            .CallId(callId)
            .SubChainId(subChainId)
            .Height(chain.Height)
            .HeadHash(chain.HeadHash)
            .KvSnapshot(chain.BuildSnapshot())
            .GroupState(groupState);
        if (sharedKey != null)
        {
            builder = builder.SharedKey(sharedKey);
        }

        return builder.Build();
    }

    private TLDto.TLGroupCallChainBlock BuildBlockRow(long callId, int subChainId,
        int height, byte[] block, byte[] blockHash) =>
        TLDto.GroupCallChainBlock.Builder()
            .CallId(callId)
            .SubChainId(subChainId)
            .Height(height)
            .Block(block)
            .BlockHash(blockHash)
            .Date(checked((int)_time.GetUnixTimeInSeconds()))
            .Build();
}
