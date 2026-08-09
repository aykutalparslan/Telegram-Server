// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Ferrite.TL;

public interface ITLStreamingObject
{
    int Constructor { get; }
    Stream Bytes { get; }
    int BytesLength { get; }

    /// <summary>
    /// Reads and discards all remaining bytes from this request's streaming pipe,
    /// including any unread body bytes, TL bytes padding, and MTProto padding.
    /// Must be called once the message has been handled - even on early-return or
    /// failure paths - otherwise an unconsumed upload body stalls the connection
    /// through pipe backpressure.
    /// </summary>
    ValueTask DrainAsync(CancellationToken cancellationToken = default);
}
