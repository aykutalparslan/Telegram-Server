// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public interface IStickerRepository
{
    bool PutSet(TLStickerSetState state);
    ValueTask<TLStickerSetState?> GetSetAsync(long setId);
    ValueTask<TLStickerSetState?> GetSetByShortNameAsync(string shortName);
    ValueTask<IReadOnlyCollection<TLStickerSetState>> GetSetsAsync();
    ValueTask<IReadOnlyCollection<TLStickerSetState>> GetOwnedSetsAsync(long ownerUserId);
    ValueTask<bool> DeleteSetAsync(long setId);

    bool PutAccountState(TLStickerAccountState state);
    ValueTask<TLStickerAccountState?> GetAccountStateAsync(long userId);
    ValueTask<IReadOnlyCollection<TLStickerAccountState>> GetAccountStatesAsync();

    bool PutChannelState(TLChannelStickerState state);
    ValueTask<TLChannelStickerState?> GetChannelStateAsync(long channelId);
    ValueTask<IReadOnlyCollection<TLChannelStickerState>> GetChannelStatesAsync();
    bool DeleteChannelState(long channelId);
}
