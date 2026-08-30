// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Utils;

namespace Ferrite.Core.Connection;

public sealed class MessageIdGenerator : IMessageIdGenerator
{
    private readonly IMTProtoTime _time;
    private long _lastMessageId;

    public MessageIdGenerator(IMTProtoTime time)
    {
        _time = time;
    }

    public long NextMessageId(bool response)
    {
        while (true)
        {
            long last = Interlocked.Read(ref _lastMessageId);
            long id = Align(_time.GetUnixTimeInSeconds() * 4294967296L, response);
            if (id <= last)
            {
                id = Align(last + 1, response);
            }

            if (Interlocked.CompareExchange(ref _lastMessageId, id, last) == last)
            {
                return id;
            }
        }
    }

    private static long Align(long value, bool response)
    {
        long remainder = response ? 1 : 3;
        return value + ((remainder - value % 4) + 4) % 4;
    }
}
