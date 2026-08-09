// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Cassandra;

namespace Ferrite.Data.Repositories;

public sealed class ImmediateWriteBatchAccessor : IWriteBatchAccessor
{
    public IWriteBatchScope BeginScope() => Scope.Instance;
    public void Enqueue(Statement statement) =>
        throw new InvalidOperationException("The local store writes through immediately");
    public void Flush() { }
    public ValueTask FlushAsync() => ValueTask.CompletedTask;

    private sealed class Scope : IWriteBatchScope
    {
        public static Scope Instance { get; } = new();
        public bool HasPendingWrites => false;
        public void Dispose() { }
    }
}
