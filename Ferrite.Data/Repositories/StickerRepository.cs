// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public sealed class StickerRepository : IStickerRepository
{
    private readonly IKVStore _sets;
    private readonly IKVStore _shortNames;
    private readonly IKVStore _ownedSets;
    private readonly IKVStore _accounts;
    private readonly IKVStore _channels;

    public StickerRepository(IKVStore sets, IKVStore shortNames,
        IKVStore ownedSets, IKVStore accounts, IKVStore channels)
    {
        _sets = sets;
        _shortNames = shortNames;
        _ownedSets = ownedSets;
        _accounts = accounts;
        _channels = channels;

        sets.SetSchema(new TableDefinition("ferrite", "sticker_sets",
            new KeyDefinition("pk",
                new DataColumn { Name = "set_id", Type = DataType.Long })));
        shortNames.SetSchema(new TableDefinition("ferrite", "sticker_short_names",
            new KeyDefinition("pk",
                new DataColumn { Name = "short_name", Type = DataType.String })));
        ownedSets.SetSchema(new TableDefinition("ferrite", "owned_sticker_sets",
            new KeyDefinition("pk",
                new DataColumn { Name = "owner_user_id", Type = DataType.Long },
                new DataColumn { Name = "set_id", Type = DataType.Long })));
        accounts.SetSchema(new TableDefinition("ferrite", "sticker_account_state",
            new KeyDefinition("pk",
                new DataColumn { Name = "user_id", Type = DataType.Long })));
        channels.SetSchema(new TableDefinition("ferrite", "channel_sticker_state",
            new KeyDefinition("pk",
                new DataColumn { Name = "channel_id", Type = DataType.Long })));
    }

    public bool PutSet(TLStickerSetState state)
    {
        var row = state.AsStickerSetState();
        byte[] bytes = state.AsSpan().ToArray();
        string shortName = Normalize(row.ShortName);
        byte[]? existingBytes = _sets.Get(row.SetId);
        if (existingBytes is { Length: > 0 })
        {
            var existing = new TLStickerSetState(existingBytes, 0,
                existingBytes.Length).AsStickerSetState();
            string existingShortName = Normalize(existing.ShortName);
            if (existingShortName != shortName)
            {
                _shortNames.Delete(existingShortName);
            }
            if (existing.OwnerUserId != row.OwnerUserId)
            {
                _ownedSets.Delete(existing.OwnerUserId, row.SetId);
            }
        }
        return _sets.Put(bytes, row.SetId) &&
               _shortNames.Put(bytes, shortName) &&
               _ownedSets.Put(bytes, row.OwnerUserId, row.SetId);
    }

    public async ValueTask<TLStickerSetState?> GetSetAsync(long setId) =>
        WrapSet(await _sets.GetAsync(setId));

    public async ValueTask<TLStickerSetState?> GetSetByShortNameAsync(
        string shortName) => WrapSet(await _shortNames.GetAsync(
        Normalize(shortName)));

    public async ValueTask<IReadOnlyCollection<TLStickerSetState>> GetSetsAsync()
    {
        var rows = new List<TLStickerSetState>();
        await foreach (byte[] bytes in _sets.IterateAsync())
        {
            rows.Add(new TLStickerSetState(bytes, 0, bytes.Length));
        }
        return rows;
    }

    public async ValueTask<IReadOnlyCollection<TLStickerSetState>> GetOwnedSetsAsync(
        long ownerUserId)
    {
        var rows = new List<TLStickerSetState>();
        await foreach (byte[] bytes in _ownedSets.IterateAsync(ownerUserId))
        {
            rows.Add(new TLStickerSetState(bytes, 0, bytes.Length));
        }
        return rows;
    }

    public async ValueTask<bool> DeleteSetAsync(long setId)
    {
        using TLStickerSetState? stored = await GetSetAsync(setId);
        if (stored is null)
        {
            return false;
        }
        var row = stored.Value.AsStickerSetState();
        long ownerUserId = row.OwnerUserId;
        string shortName = Normalize(row.ShortName);
        return _sets.Delete(setId) && _shortNames.Delete(shortName) &&
               _ownedSets.Delete(ownerUserId, setId);
    }

    public bool PutAccountState(TLStickerAccountState state)
    {
        var row = state.AsStickerAccountState();
        return _accounts.Put(state.AsSpan().ToArray(), row.UserId);
    }

    public async ValueTask<TLStickerAccountState?> GetAccountStateAsync(long userId) =>
        WrapAccount(await _accounts.GetAsync(userId));

    public async ValueTask<IReadOnlyCollection<TLStickerAccountState>>
        GetAccountStatesAsync()
    {
        var rows = new List<TLStickerAccountState>();
        await foreach (byte[] bytes in _accounts.IterateAsync())
        {
            rows.Add(new TLStickerAccountState(bytes, 0, bytes.Length));
        }
        return rows;
    }

    public bool PutChannelState(TLChannelStickerState state)
    {
        var row = state.AsChannelStickerState();
        return _channels.Put(state.AsSpan().ToArray(), row.ChannelId);
    }

    public async ValueTask<TLChannelStickerState?> GetChannelStateAsync(long channelId) =>
        WrapChannel(await _channels.GetAsync(channelId));

    public async ValueTask<IReadOnlyCollection<TLChannelStickerState>>
        GetChannelStatesAsync()
    {
        var rows = new List<TLChannelStickerState>();
        await foreach (byte[] bytes in _channels.IterateAsync())
        {
            rows.Add(new TLChannelStickerState(bytes, 0, bytes.Length));
        }
        return rows;
    }

    public bool DeleteChannelState(long channelId) => _channels.Delete(channelId);

    private static string Normalize(ReadOnlySpan<byte> shortName) =>
        Encoding.UTF8.GetString(shortName).ToLowerInvariant();

    private static string Normalize(string shortName) => shortName.ToLowerInvariant();

    private static TLStickerSetState? WrapSet(byte[]? bytes) =>
        bytes is { Length: > 0 } ? new TLStickerSetState(bytes, 0, bytes.Length) : null;

    private static TLStickerAccountState? WrapAccount(byte[]? bytes) =>
        bytes is { Length: > 0 }
            ? new TLStickerAccountState(bytes, 0, bytes.Length) : null;

    private static TLChannelStickerState? WrapChannel(byte[]? bytes) =>
        bytes is { Length: > 0 }
            ? new TLChannelStickerState(bytes, 0, bytes.Length) : null;
}
