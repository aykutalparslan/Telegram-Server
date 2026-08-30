// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public sealed class ChannelAdminRepository : IChannelAdminRepository
{
    private readonly IKVStore _states;
    private readonly IKVStore _slowMode;

    public ChannelAdminRepository(IKVStore states, IKVStore slowMode)
    {
        _states = states;
        _slowMode = slowMode;

        states.SetSchema(new TableDefinition("ferrite", "channel_admin_state",
            new KeyDefinition("pk",
                new DataColumn { Name = "channel_id", Type = DataType.Long })));
        slowMode.SetSchema(new TableDefinition("ferrite", "channel_slow_mode_state",
            new KeyDefinition("pk",
                new DataColumn { Name = "channel_id", Type = DataType.Long },
                new DataColumn { Name = "user_id", Type = DataType.Long })));
    }

    public bool PutState(TLChannelAdminState state) =>
        _states.Put(state.AsSpan().ToArray(),
            state.AsChannelAdminState().ChannelId);

    public async ValueTask<TLChannelAdminState?> GetStateAsync(long channelId) =>
        WrapState(await _states.GetAsync(channelId));

    public bool DeleteState(long channelId) => _states.Delete(channelId);

    public async ValueTask<IReadOnlyCollection<TLChannelAdminState>> GetStatesAsync()
    {
        var rows = new List<TLChannelAdminState>();
        await foreach (byte[] bytes in _states.IterateAsync())
        {
            rows.Add(new TLChannelAdminState(bytes, 0, bytes.Length));
        }
        return rows;
    }

    public bool PutSlowModeState(TLChannelSlowModeState state)
    {
        var row = state.AsChannelSlowModeState();
        return _slowMode.Put(state.AsSpan().ToArray(), row.ChannelId, row.UserId);
    }

    public async ValueTask<TLChannelSlowModeState?> GetSlowModeStateAsync(
        long channelId, long userId) =>
        WrapSlowMode(await _slowMode.GetAsync(channelId, userId));

    public bool DeleteSlowModeState(long channelId, long userId) =>
        _slowMode.Delete(channelId, userId);

    public async ValueTask<bool> DeleteSlowModeStatesAsync(long channelId)
    {
        var userIds = new List<long>();
        await foreach (byte[] bytes in _slowMode.IterateAsync(channelId))
        {
            using var row = new TLChannelSlowModeState(bytes, 0, bytes.Length);
            userIds.Add(row.AsChannelSlowModeState().UserId);
        }

        bool deleted = false;
        foreach (long userId in userIds)
        {
            deleted |= _slowMode.Delete(channelId, userId);
        }
        return deleted;
    }

    private static TLChannelAdminState? WrapState(byte[]? bytes) =>
        bytes is { Length: > 0 }
            ? new TLChannelAdminState(bytes, 0, bytes.Length) : null;

    private static TLChannelSlowModeState? WrapSlowMode(byte[]? bytes) =>
        bytes is { Length: > 0 }
            ? new TLChannelSlowModeState(bytes, 0, bytes.Length) : null;
}
