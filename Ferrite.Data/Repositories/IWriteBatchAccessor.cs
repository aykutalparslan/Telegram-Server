// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Cassandra;

namespace Ferrite.Data.Repositories;

public interface IWriteBatchScope : IDisposable
{
    bool HasPendingWrites { get; }
}

public interface IWriteBatchAccessor
{
    IWriteBatchScope BeginScope();
    void Enqueue(Statement statement);
    void Flush();
    ValueTask FlushAsync();
}
