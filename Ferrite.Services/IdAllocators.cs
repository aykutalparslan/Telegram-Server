// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;

namespace Ferrite.Services;

// Process-wide monotonic sources shared by capability services and the
// collaborators they are being decomposed into. This class must remain a
// singleton: per-operation instances would duplicate logical-message and
// reaction-order sequences.
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

    // Group-call ids come from a counter rather than a random draw so a creation
    // never has to retry around a collision with a live call.
    public ValueTask<long> NextGroupCallIdAsync() => _groupCallIds.IncrementAndGet();

    // A poll id is the client's global handle for one poll, so it must be unique
    // across every dialog rather than per message box.
    public ValueTask<long> NextPollIdAsync() => _pollIds.IncrementAndGet();
}
