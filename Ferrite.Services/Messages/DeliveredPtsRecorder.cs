// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC


namespace Ferrite.Services.Messages;

public sealed class DeliveredPtsRecorder
{
    private readonly IUpdatesContextFactory _updatesContextFactory;

    public DeliveredPtsRecorder(IUpdatesContextFactory updatesContextFactory)
    {
        _updatesContextFactory = updatesContextFactory;
    }

    public async Task RecordAsync(MTProtoMessage message)
    {
        if (message.RecipientUserId is not { } userId || message.Pts is not { } pts)
        {
            return;
        }
        await _updatesContextFactory.GetUpdatesContext(null, userId)
            .AdvanceDeliveredPts(pts);
    }
}
