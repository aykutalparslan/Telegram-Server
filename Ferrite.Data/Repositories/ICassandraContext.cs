// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Cassandra;

namespace Ferrite.Data.Repositories;

public interface ICassandraContext
{
    bool TableExists(string keyspace, string table);
    void Enqueue(Statement statement);
    RowSet Execute(Statement statement);
    Task<RowSet> ExecuteAsync(Statement statement);
}
