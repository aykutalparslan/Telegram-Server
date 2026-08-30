// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public interface IChannelAdminRepository
{
    public bool PutState(TLChannelAdminState state);
    public ValueTask<TLChannelAdminState?> GetStateAsync(long channelId);
    public bool DeleteState(long channelId);

    public ValueTask<IReadOnlyCollection<TLChannelAdminState>> GetStatesAsync();

    public bool PutSlowModeState(TLChannelSlowModeState state);

    public ValueTask<TLChannelSlowModeState?> GetSlowModeStateAsync(
        long channelId, long userId);

    public bool DeleteSlowModeState(long channelId, long userId);

    public ValueTask<bool> DeleteSlowModeStatesAsync(long channelId);
}
