// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public interface IStatisticsRepository
{
    bool PutPublicForward(TLPublicForwardRef row);

    ValueTask<IReadOnlyCollection<TLPublicForwardRef>> GetPublicForwardsAsync(
        long channelId, int msgId);

    bool PutGraphToken(TLStatsGraphToken row);

    ValueTask<TLStatsGraphToken?> GetGraphTokenAsync(string token);
}
