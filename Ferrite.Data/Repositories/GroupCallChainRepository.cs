// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public sealed class GroupCallChainRepository : IGroupCallChainRepository
{
    private const int StripeCount = 256;

    private readonly IKVStore _states;
    private readonly IKVStore _blocks;
    private readonly Func<ValueTask<bool>> _flush;
    private readonly SemaphoreSlim[] _chainGates = CreateGates();

    public GroupCallChainRepository(IKVStore states, IKVStore blocks,
        Func<ValueTask<bool>>? flush = null)
    {
        _states = states;
        states.SetSchema(new TableDefinition("ferrite", "group_call_chain_states",
            new KeyDefinition("pk",
                new DataColumn { Name = "call_id", Type = DataType.Long },
                new DataColumn { Name = "sub_chain_id", Type = DataType.Int })));
        _blocks = blocks;
        blocks.SetSchema(new TableDefinition("ferrite", "group_call_chain_blocks",
            new KeyDefinition("pk",
                new DataColumn { Name = "call_id", Type = DataType.Long },
                new DataColumn { Name = "sub_chain_id", Type = DataType.Int },
                new DataColumn { Name = "height", Type = DataType.Int })));
        _flush = flush ?? (() => ValueTask.FromResult(true));
    }

    private static SemaphoreSlim[] CreateGates() =>
        Enumerable.Range(0, StripeCount).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    private SemaphoreSlim GetChainGate(long callId, int subChainId) =>
        _chainGates[(int)(unchecked((ulong)(callId * 2 + subChainId)) %
                          (uint)_chainGates.Length)];

    private async ValueTask FlushAsync(string operation)
    {
        if (!await _flush())
        {
            throw new IOException($"Failed to persist {operation}.");
        }
    }

    private static TLDto.TLGroupCallChainState ReadState(byte[] bytes) =>
        new(bytes, 0, bytes.Length);

    private static TLDto.TLGroupCallChainBlock ReadBlock(byte[] bytes) =>
        new(bytes, 0, bytes.Length);

    private async ValueTask<TLDto.TLGroupCallChainState?> GetStateInternalAsync(long callId,
        int subChainId)
    {
        byte[]? bytes = await _states.GetAsync(callId, subChainId);
        return bytes == null ? null : ReadState(bytes);
    }

    private async ValueTask<TLDto.TLGroupCallChainBlock?> GetBlockInternalAsync(long callId,
        int subChainId, int height)
    {
        byte[]? bytes = await _blocks.GetAsync(callId, subChainId, height);
        return bytes == null ? null : ReadBlock(bytes);
    }

    private async ValueTask<int> GetHeightInternalAsync(long callId, int subChainId)
    {
        using TLDto.TLGroupCallChainState? state =
            await GetStateInternalAsync(callId, subChainId);
        // A chain nobody has written to sits at the synthetic genesis height -1.
        return state == null ? -1 : state.Value.AsGroupCallChainState().Height;
    }

    public async ValueTask<TLDto.TLGroupCallChainState?> GetChainStateAsync(long callId,
        int subChainId, CancellationToken cancellationToken = default) =>
        await GetStateInternalAsync(callId, subChainId);

    // Append is the fork-prevention point. The whole reason the server exists in
    // this protocol is that exactly one block wins at each height, so the height
    // check and the write happen under the same per-(call, sub-chain) gate. A
    // caller that validated against a stale head loses here rather than forking.
    public async ValueTask<GroupCallChainAppendResult> TryAppendBlockAsync(
        TLDto.TLGroupCallChainState newState, TLDto.TLGroupCallChainBlock block,
        int expectedHeight, CancellationToken cancellationToken = default)
    {
        var stateView = newState.AsGroupCallChainState();
        var blockView = block.AsGroupCallChainBlock();
        long callId = stateView.CallId;
        int subChainId = stateView.SubChainId;
        int height = stateView.Height;
        if (blockView.CallId != callId || blockView.SubChainId != subChainId ||
            blockView.Height != height)
        {
            throw new ArgumentException(
                "The head state and the appended block must describe the same block.",
                nameof(block));
        }

        SemaphoreSlim chainGate = GetChainGate(callId, subChainId);
        await chainGate.WaitAsync(cancellationToken);
        try
        {
            int current = await GetHeightInternalAsync(callId, subChainId);
            if (current != expectedHeight)
            {
                return new GroupCallChainAppendResult(false, current);
            }
            _blocks.Put(block.AsSpan().ToArray(), callId, subChainId, height);
            _states.Put(newState.AsSpan().ToArray(), callId, subChainId);
            await FlushAsync("group call chain block");
            return new GroupCallChainAppendResult(true, height);
        }
        finally
        {
            chainGate.Release();
        }
    }

    // Blocks are indexed by (call_id, sub_chain_id, height) and height IS the
    // offset TDLib polls with, so a window is a contiguous height range. An
    // offset past the head is an empty page, never an error: TDLib polls
    // speculatively after every update.
    public async ValueTask<IReadOnlyList<TLDto.TLGroupCallChainBlock>> GetBlocksAsync(
        long callId, int subChainId, int offset, int limit,
        CancellationToken cancellationToken = default)
    {
        if (offset < 0 || limit <= 0)
        {
            return Array.Empty<TLDto.TLGroupCallChainBlock>();
        }
        List<TLDto.TLGroupCallChainBlock> window = new();
        try
        {
            // Heights are contiguous from 0 because every append writes exactly
            // expectedHeight + 1, so the first miss is the end of the chain and
            // an unbounded limit costs one lookup past the head.
            for (int i = 0; i < limit; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                TLDto.TLGroupCallChainBlock? block =
                    await GetBlockInternalAsync(callId, subChainId, offset + i);
                if (block == null)
                {
                    break;
                }
                window.Add(block.Value);
            }
        }
        catch
        {
            foreach (TLDto.TLGroupCallChainBlock block in window) block.Dispose();
            throw;
        }
        return window;
    }

    public async ValueTask<TLDto.TLGroupCallChainBlock?> GetLastBlockAsync(long callId,
        int subChainId, CancellationToken cancellationToken = default)
    {
        int height = await GetHeightInternalAsync(callId, subChainId);
        return height < 0 ? null : await GetBlockInternalAsync(callId, subChainId, height);
    }

    public async ValueTask<int> GetNextOffsetAsync(long callId, int subChainId,
        CancellationToken cancellationToken = default) =>
        await GetHeightInternalAsync(callId, subChainId) + 1;

    // Discard drops the whole call prefix. Both sub-chain gates are held in a
    // fixed order so an append that is mid-commit cannot leave half a chain
    // behind the delete.
    public async ValueTask DeleteChainAsync(long callId,
        CancellationToken cancellationToken = default)
    {
        SemaphoreSlim blockGate = GetChainGate(callId, GroupCallSubChain.Blocks);
        SemaphoreSlim broadcastGate = GetChainGate(callId, GroupCallSubChain.Broadcast);
        await blockGate.WaitAsync(cancellationToken);
        bool sharedGate = ReferenceEquals(blockGate, broadcastGate);
        if (!sharedGate) await broadcastGate.WaitAsync(cancellationToken);
        try
        {
            _blocks.Delete(callId);
            _states.Delete(callId);
            await FlushAsync("group call chain deletion");
        }
        finally
        {
            if (!sharedGate) broadcastGate.Release();
            blockGate.Release();
        }
    }
}
