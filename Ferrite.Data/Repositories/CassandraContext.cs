// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Cassandra;

namespace Ferrite.Data.Repositories;

public sealed class CassandraContext : ICassandraContext, IDisposable
{
    private readonly Cluster _cluster;
    private readonly ISession _session;
    private readonly string _keySpace;

    public CassandraContext(string keyspace, int port, params string[] hosts)
    {
        _cluster = Cluster.Builder()
            .AddContactPoints(hosts)
            .WithPort(port)
            .Build();

        _keySpace = keyspace;
        _session = _cluster.Connect();
        Dictionary<string, string> replication = new Dictionary<string, string>();
        replication.Add("class", "SimpleStrategy");
        replication.Add("replication_factor", "1");
        _session.CreateKeyspaceIfNotExists(_keySpace, replication);
    }

    public bool TableExists(string keyspace, string table) =>
        _cluster.Metadata.GetTable(keyspace, table) != null;
    
    public void Enqueue(Statement statement)
    {
        throw new InvalidOperationException(
            "Cassandra writes require a request-scoped IWriteBatchAccessor");
    }
    public RowSet Execute(Statement statement)
    {
        return _session.Execute(statement);
    }
    public async Task<RowSet> ExecuteAsync(Statement statement)
    {
        return await _session.ExecuteAsync(statement);
    }
    public void Dispose()
    {
        _session.Dispose();
        _cluster.Dispose();
    }
}
