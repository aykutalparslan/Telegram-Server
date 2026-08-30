// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Primitives;

namespace Ferrite.Data.Counters;

public class FasterCounterFactory : ICounterFactory, IAsyncDisposable
{
    private readonly FasterContext<string, long> _context;
    public FasterCounterFactory(string path)
    {
        _context = new FasterContext<string, long>(path);
    }
    public IAtomicCounter GetCounter(string name)
    {
        return new FasterCounter(_context, name);
    }

    public ValueTask DisposeAsync() => _context.DisposeAsync();
}
