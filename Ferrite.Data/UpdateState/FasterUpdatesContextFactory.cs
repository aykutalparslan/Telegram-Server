// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Primitives;

namespace Ferrite.Data.UpdateState;

public class FasterUpdatesContextFactory : IUpdatesContextFactory, IAsyncDisposable
{
    private readonly FasterContext<string, long> _counterContext;
    private readonly FasterContext<string, SortedSet<long>> _unreadContext;
    private readonly FasterContext<string, SortedSet<string>> _dialogContex;
    private readonly FasterContext<string, byte[]> _settledPtsContext;
    public FasterUpdatesContextFactory(string path)
    {
        _counterContext = new FasterContext<string, long>(path + "-counter");
        _unreadContext = new FasterContext<string, SortedSet<long>>(path + "-unread");
        _dialogContex = new FasterContext<string, SortedSet<string>>(path + "-dialog");
        _settledPtsContext = new FasterContext<string, byte[]>(path + "-settled-pts");
    }
    public IUpdatesContext GetUpdatesContext(long? authKeyId, long userId)
    {
        return new FasterUpdatesContext(_counterContext, _unreadContext, _dialogContex, _settledPtsContext,
            authKeyId, userId);
    }

    public async ValueTask DisposeAsync()
    {
        await _counterContext.DisposeAsync();
        await _unreadContext.DisposeAsync();
        await _dialogContex.DisposeAsync();
        await _settledPtsContext.DisposeAsync();
    }
}
