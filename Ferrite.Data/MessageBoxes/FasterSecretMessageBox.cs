// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Counters;
using Ferrite.Data.Primitives;

namespace Ferrite.Data.MessageBoxes;

public class FasterSecretMessageBox : ISecretMessageBox, IAsyncDisposable
{
    private readonly IAtomicCounter _counter;
    private readonly long _authKeyId;
    public FasterSecretMessageBox(FasterContext<string, long> counterContext, long authKeyId)
    {
        _authKeyId = authKeyId;
        _counter = new FasterCounter(counterContext , $"seq:qts:{authKeyId}");
    }
    public async ValueTask<int> Qts()
    {
        return (int)await _counter.IncrementTo(1);
    }

    public async ValueTask<int> IncrementQts()
    {
        await _counter.IncrementTo(1);
        return (int)await _counter.IncrementAndGet();
    }

    public async ValueTask DisposeAsync()
    {
        await _counter.DisposeAsync();
    }
}
