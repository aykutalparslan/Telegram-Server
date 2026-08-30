// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Common;

public sealed class IdAllocators
{
    private readonly IChatIdAllocator _chatIds;
    private readonly IAtomicCounter _logicalMessageIds;
    private readonly IAtomicCounter _reactionOrders;
    private readonly IAtomicCounter _mediaGroupIds;
    private readonly IAtomicCounter _groupCallIds;
    private readonly IAtomicCounter _pollIds;

    public IdAllocators(IChatIdAllocator chatIds, ICounterFactory counterFactory)
    {
        _chatIds = chatIds;
        _logicalMessageIds = counterFactory.GetCounter("counter_logical_message_id");
        _reactionOrders = counterFactory.GetCounter("counter_reaction_order");
        _mediaGroupIds = counterFactory.GetCounter("counter_media_group_id");
        _groupCallIds = counterFactory.GetCounter("counter_group_call_id");
        _pollIds = counterFactory.GetCounter("counter_poll_id");
    }

    public ValueTask<long> NextChatIdAsync() => _chatIds.NextIdAsync();

    public ValueTask<long> NextLogicalMessageIdAsync() =>
        _logicalMessageIds.IncrementAndGet();

    public ValueTask<long> NextReactionOrderAsync() =>
        _reactionOrders.IncrementAndGet();

    public ValueTask<long> NextMediaGroupIdAsync() =>
        _mediaGroupIds.IncrementAndGet();

    public ValueTask<long> NextGroupCallIdAsync() => _groupCallIds.IncrementAndGet();

    public ValueTask<long> NextPollIdAsync() => _pollIds.IncrementAndGet();
}
