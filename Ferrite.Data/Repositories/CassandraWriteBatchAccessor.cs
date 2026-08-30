// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Cassandra;

namespace Ferrite.Data.Repositories;

public sealed class CassandraWriteBatchAccessor : IWriteBatchAccessor
{
    private readonly ICassandraContext _context;
    private readonly string _keyspace;
    private readonly AsyncLocal<BatchScope?> _current = new();

    public CassandraWriteBatchAccessor(ICassandraContext context, string keyspace)
    {
        _context = context;
        _keyspace = keyspace;
    }

    public IWriteBatchScope BeginScope()
    {
        var scope = new BatchScope(this, _current.Value);
        _current.Value = scope;
        return scope;
    }

    public void Enqueue(Statement statement) => Current().Statements.Enqueue(statement);

    public void Flush()
    {
        BatchScope scope = Current();
        Statement? statement = Drain(scope);
        if (statement != null) _context.Execute(statement);
    }

    public async ValueTask FlushAsync()
    {
        BatchScope scope = Current();
        Statement? statement = Drain(scope);
        if (statement != null) await _context.ExecuteAsync(statement);
    }

    private BatchScope Current()
    {
        if (_current.Value is { } scope) return scope;
        scope = new BatchScope(this, null, implicitScope: true);
        _current.Value = scope;
        return scope;
    }

    private Statement? Drain(BatchScope scope)
    {
        if (scope.Statements.Count == 0) return null;
        if (scope.Statements.Count == 1) return scope.Statements.Dequeue();
        var batch = new BatchStatement().SetBatchType(BatchType.Logged);
        while (scope.Statements.TryDequeue(out Statement? statement))
        {
            batch.Add(statement);
        }
        return batch.SetKeyspace(_keyspace);
    }

    private sealed class BatchScope : IWriteBatchScope
    {
        private readonly CassandraWriteBatchAccessor _owner;
        private readonly BatchScope? _previous;
        private readonly bool _implicit;
        private bool _disposed;

        public BatchScope(CassandraWriteBatchAccessor owner, BatchScope? previous,
            bool implicitScope = false)
        {
            _owner = owner;
            _previous = previous;
            _implicit = implicitScope;
        }

        public Queue<Statement> Statements { get; } = new();
        public bool HasPendingWrites => Statements.Count != 0;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            bool pending = HasPendingWrites;
            if (!_implicit) _owner._current.Value = _previous;
            if (pending)
            {
                throw new InvalidOperationException(
                    "A storage write scope ended without exactly one flush");
            }
        }
    }
}
