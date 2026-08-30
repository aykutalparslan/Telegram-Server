// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public interface IChannelAdminLogRepository
{
    public bool PutEvent(TLAdminLogEvent row);

    public ValueTask<IReadOnlyCollection<TLAdminLogEvent>> GetEventsAsync(
        long channelId);
}
