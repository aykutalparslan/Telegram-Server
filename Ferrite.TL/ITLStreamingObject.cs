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

    ValueTask DrainAsync(CancellationToken cancellationToken = default);
}
