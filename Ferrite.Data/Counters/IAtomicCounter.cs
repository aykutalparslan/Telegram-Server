// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System;
namespace Ferrite.Data.Counters;

public interface IAtomicCounter : IAsyncDisposable
{
    public ValueTask<long> Get();
    public ValueTask<long> IncrementAndGet();
    public ValueTask<long> IncrementByAndGet(long inc);
    public ValueTask<long> IncrementTo(long val);
}

