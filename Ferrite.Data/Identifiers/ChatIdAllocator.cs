// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Counters;

namespace Ferrite.Data.Identifiers;

public class ChatIdAllocator : IChatIdAllocator
{
    private readonly IAtomicCounter _chatIdCounter;

    public ChatIdAllocator(ICounterFactory counterFactory)
    {
        _chatIdCounter = counterFactory.GetCounter("counter_chat_id");
    }

    public async ValueTask<long> NextIdAsync()
    {
        long chatId = await _chatIdCounter.IncrementAndGet();
        if (chatId == 0)
        {
            chatId = await _chatIdCounter.IncrementAndGet();
        }

        return chatId;
    }
}
