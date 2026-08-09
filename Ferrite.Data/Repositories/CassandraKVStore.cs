// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Cassandra;
using Ferrite.Data.Repositories;

namespace Ferrite.Data.Repositories;

public class CassandraKVStore : IKVStore
{
    private static long _lastTimestampMicros;
    private readonly ICassandraContext _context;
    private readonly Action<Statement> _enqueue;
    private TableDefinition _table;
    private const string IntStr = "int";
    private const string BoolStr = "boolean";
    private const string LongStr = "bigint";
    private const string FloatStr = "float";
    private const string DoubleStr = "double";
    private const string DateStr = "date";
    private const string StringStr = "text";
    private const string BytesStr = "blob";
    private static string GetTypeStr(DataType type) => type switch
    {
        DataType.Bool => BoolStr,
        DataType.Int => IntStr,
        DataType.Long => LongStr,
        DataType.Float => FloatStr,
        DataType.Double => DoubleStr,
        DataType.DateTime => DateStr,
        DataType.String => StringStr,
        DataType.Bytes => BytesStr,
        _ => ""
    };
    private static Type GetManagedType(DataType type) => type switch
    {
        DataType.Bool => typeof(bool),
        DataType.Int => typeof(int),
        DataType.Long => typeof(long),
        DataType.Float => typeof(float),
        DataType.Double => typeof(double),
        DataType.DateTime => typeof(DateTime),
        DataType.String => typeof(string),
        DataType.Bytes => typeof(byte[]),
        _ => typeof(object)
    };
    public CassandraKVStore(ICassandraContext context)
    {
        _context = context;
        _enqueue = context.Enqueue;
    }

    public CassandraKVStore(ICassandraContext context,
        IWriteBatchAccessor writeBatches)
    {
        _context = context;
        _enqueue = writeBatches.Enqueue;
    }

    public void SetSchema(TableDefinition table)
    {
        _table = table;
        StringBuilder sb = new StringBuilder($"CREATE TABLE IF NOT EXISTS {_table.Keyspace}.{_table.Name} (");
        bool first = true;
        int pcount = 0;
        foreach (var c in _table.PrimaryKey.Columns)
        {
            pcount++;
            if (!first)
            {
                sb.Append(", ");
            }
            first = false;
            sb.Append($"{c.Name} {GetTypeStr(c.Type)}");
        }

        if (pcount > 0)
        {
            sb.Append(", ");
        }
        pcount = 0;
        sb.Append($"{_table.Name}_data blob, ");
        sb.Append("PRIMARY KEY (");
        first = true;
        foreach (var c in _table.PrimaryKey.Columns)
        {
            if (!first)
            {
                sb.Append(", ");
            }
            first = false;
            sb.Append($"{c.Name}");
        }
        sb.Append("));");
        var statement = new SimpleStatement(sb.ToString());
        _context.Execute(statement);
        foreach (var sc in _table.SecondaryIndices)
        {
            pcount = 0;
            sb = new StringBuilder($"CREATE TABLE IF NOT EXISTS {_table.Keyspace}.{_table.Name}_{sc.Name} (");
            first = true;
            foreach (var c in sc.Columns)
            {
                pcount++;
                if (!first)
                {
                    sb.Append(", ");
                }
                first = false;
                sb.Append($"{c.Name} {GetTypeStr(c.Type)}");
            }
            first = true;
            if (pcount > 0)
            {
                sb.Append(", ");
            }
            pcount = 0;
            foreach (var c in _table.PrimaryKey.Columns)
            {
                pcount++;
                if (!first)
                {
                    sb.Append(", ");
                }
                first = false;
                sb.Append($"pk_{c.Name} {GetTypeStr(c.Type)}");
            }
            if (pcount > 0)
            {
                sb.Append(", ");
            }
            sb.Append("PRIMARY KEY (");
            first = true;
            foreach (var c in sc.Columns)
            {
                if (!first)
                {
                    sb.Append(", ");
                }
                first = false;
                sb.Append($"{c.Name}");
            }
            sb.Append("));");
            statement = new SimpleStatement(sb.ToString());
            _context.Execute(statement);
        }
    }

    public bool Put(byte[] data, params object[] keys)
    {
        if (keys.Length != _table.PrimaryKey.Columns.Count)
        {
            throw new Exception("Parameter count mismatch.");
        }
        StringBuilder sb = new StringBuilder($"UPDATE {_table.Keyspace}.{_table.Name} " +
                                             $"USING TIMESTAMP ? SET {_table.Name}_data = ? ");
        sb.Append($"WHERE ");
        bool first = true;
        for (int i = 0; i < keys.Length; i++)
        {
            var col = _table.PrimaryKey.Columns[i];
            if (keys[i].GetType() != GetManagedType(col.Type))
            {
                throw new Exception($"Expected type was {GetManagedType(col.Type)} and " +
                                    $"the parameter was of type {keys[i].GetType()}");
            }
            if (!first)
            {
                sb.Append($" AND ");
            }
            first = false;
            sb.Append($"{col.Name} = ?");
        }

        List<object> p = new List<object> { NextTimestampMicros() };
        p.Add(data);
        p.AddRange(keys);
        var statement = new SimpleStatement(sb.ToString(), p.ToArray());
        _enqueue(statement);
        
        foreach (var sc in _table.SecondaryIndices)
        {
            first = true;
            List<object> secondaryParams = new();
            foreach (var c in sc.Columns)
            {
                secondaryParams.Add(keys[_table.PrimaryKey.GetOrdinal(c.Name)]);
            }
            sb = new StringBuilder($"UPDATE {_table.Keyspace}.{_table.Name}_{sc.Name} " +
                                   "USING TIMESTAMP ? SET ");
            foreach (var c in _table.PrimaryKey.Columns)
            {
                if (!first)
                {
                    sb.Append(", ");
                }
                first = false;
                sb.Append($"pk_{c.Name} = ?");
            }
            sb.Append($" WHERE ");
            first = true;
            for (int i = 0; i < secondaryParams.Count; i++)
            {
                var col = sc.Columns[i];
                if (!first)
                {
                    sb.Append($" AND ");
                }
                first = false;
                sb.Append($"{col.Name} = ?");
            }
            List<object> p2 = new List<object> { NextTimestampMicros() };
            p2.AddRange(keys);
            p2.AddRange(secondaryParams);
            var indexStatement = new SimpleStatement(sb.ToString(), p2.ToArray());
            _enqueue(indexStatement);
        }

        return true;
    }

    public bool Delete(params object[] keys)
    {
        EnqueueDelete(keys);
        return true;
    }

    public ValueTask<bool> DeleteAsync(params object[] keys)
    {
        EnqueueDelete(keys);
        return new ValueTask<bool>(true);
    }

    // Deletes the matching main row(s) and every secondary-index row that
    // references them, mirroring the RocksDB store semantics. Index rows must
    // be resolved before the main rows are removed.
    private void EnqueueDelete(object[] keys)
    {
        StringBuilder sb = new StringBuilder($"DELETE FROM {_table.Keyspace}.{_table.Name} " +
                                             "USING TIMESTAMP ? WHERE ");
        bool first = true;
        for (int i = 0; i < keys.Length; i++)
        {
            var col = _table.PrimaryKey.Columns[i];
            if (keys[i].GetType() != GetManagedType(col.Type))
            {
                throw new Exception($"Expected type was {GetManagedType(col.Type)} and " +
                                    $"the parameter was of type {keys[i].GetType()}");
            }
            if (!first)
            {
                sb.Append($" AND ");
            }
            first = false;
            sb.Append($"{col.Name} = ?");
        }
        var parameters = new List<object> { NextTimestampMicros() };
        parameters.AddRange(keys);
        var statement = new SimpleStatement(sb.ToString(), parameters.ToArray());
        EnqueueSecondaryIndexDeletes(keys);
        _enqueue(statement);
    }

    private void EnqueueSecondaryIndexDeletes(object[] keys)
    {
        if (_table.SecondaryIndices.Count == 0)
        {
            return;
        }
        if (keys.Length == _table.PrimaryKey.Columns.Count)
        {
            foreach (var sc in _table.SecondaryIndices)
            {
                List<object> secondaryParams = new();
                foreach (var c in sc.Columns)
                {
                    secondaryParams.Add(keys[_table.PrimaryKey.GetOrdinal(c.Name)]);
                }
                EnqueueIndexRowDelete(sc, secondaryParams);
            }
        }
        else
        {
            foreach (var row in SelectRows(keys))
            {
                foreach (var sc in _table.SecondaryIndices)
                {
                    List<object> secondaryParams = new();
                    foreach (var c in sc.Columns)
                    {
                        secondaryParams.Add(row.GetValue(GetManagedType(c.Type), c.Name));
                    }
                    EnqueueIndexRowDelete(sc, secondaryParams);
                }
            }
        }
    }

    private void EnqueueIndexRowDelete(KeyDefinition sc, List<object> secondaryParams)
    {
        StringBuilder sb = new StringBuilder($"DELETE FROM {_table.Keyspace}.{_table.Name}_{sc.Name} " +
                                             "USING TIMESTAMP ? WHERE ");
        bool first = true;
        for (int i = 0; i < secondaryParams.Count; i++)
        {
            var col = sc.Columns[i];
            if (!first)
            {
                sb.Append($" AND ");
            }
            first = false;
            sb.Append($"{col.Name} = ?");
        }
        var parameters = new List<object> { NextTimestampMicros() };
        parameters.AddRange(secondaryParams);
        var statement = new SimpleStatement(sb.ToString(), parameters.ToArray());
        _enqueue(statement);
    }

    private static long NextTimestampMicros()
    {
        long observed;
        long next;
        do
        {
            observed = Volatile.Read(ref _lastTimestampMicros);
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 1_000;
            next = Math.Max(now, observed + 1);
        }
        while (Interlocked.CompareExchange(ref _lastTimestampMicros, next,
                   observed) != observed);

        return next;
    }

    private IEnumerable<Row> SelectRows(object[] keys)
    {
        StringBuilder sb = new StringBuilder($"SELECT * FROM {_table.Keyspace}.{_table.Name} WHERE ");
        bool first = true;
        for (int i = 0; i < keys.Length; i++)
        {
            var col = _table.PrimaryKey.Columns[i];
            if (keys[i].GetType() != GetManagedType(col.Type))
            {
                throw new Exception($"Expected type was {GetManagedType(col.Type)} and " +
                                    $"the parameter was of type {keys[i].GetType()}");
            }
            if (!first)
            {
                sb.Append($" AND ");
            }
            first = false;
            sb.Append($"{col.Name} = ?");
        }
        var statement = new SimpleStatement(sb.ToString(), keys.ToArray());
        var results = _context.Execute(statement);
        if (results == null)
        {
            return Enumerable.Empty<Row>();
        }
        return results;
    }

    public bool DeleteBySecondaryIndex(string indexName, params object[] keys)
    {
        var sc = _table.SecondaryIndices.FirstOrDefault(x=>x.Name == indexName);
        if (sc == null)
        {
            return false;
        }
        var statement = new SimpleStatement(BuildIndexSelect(sc, keys), keys.ToArray());
        var results = _context.Execute(statement);
        if (results == null)
        {
            return false;
        }
        var row = results.FirstOrDefault();
        if (row == null)
        {
            return false;
        }
        EnqueueDelete(PrimaryKeyFromIndexRow(row));
        return true;
    }

    public async ValueTask<bool> DeleteBySecondaryIndexAsync(string indexName, params object[] keys)
    {
        var sc = _table.SecondaryIndices.FirstOrDefault(x=>x.Name == indexName);
        if (sc == null)
        {
            return false;
        }
        var statement = new SimpleStatement(BuildIndexSelect(sc, keys), keys.ToArray());
        var results = await _context.ExecuteAsync(statement);
        if (results == null)
        {
            return false;
        }
        var row = results.FirstOrDefault();
        if (row == null)
        {
            return false;
        }
        EnqueueDelete(PrimaryKeyFromIndexRow(row));
        return true;
    }

    private string BuildIndexSelect(KeyDefinition sc, object[] keys)
    {
        StringBuilder sb = new StringBuilder($"SELECT * FROM {_table.Keyspace}.{_table.Name}_{sc.Name} WHERE ");
        bool first = true;
        for (int i = 0; i < keys.Length; i++)
        {
            var col = sc.Columns[i];
            if (keys[i].GetType() != GetManagedType(col.Type))
            {
                throw new Exception($"Expected type was {GetManagedType(col.Type)} and " +
                                    $"the parameter was of type {keys[i].GetType()}");
            }
            if (!first)
            {
                sb.Append($" AND ");
            }
            first = false;
            sb.Append($"{col.Name} = ?");
        }
        return sb.ToString();
    }

    // Index tables store the referenced primary key in pk_-prefixed columns.
    private object[] PrimaryKeyFromIndexRow(Row row)
    {
        List<object> primaryParameters = new();
        foreach (var c in _table.PrimaryKey.Columns)
        {
            primaryParameters.Add(row.GetValue(GetManagedType(c.Type), $"pk_{c.Name}"));
        }
        return primaryParameters.ToArray();
    }

    public byte[]? Get(params object[] keys)
    {
        StringBuilder sb = new StringBuilder($"SELECT * FROM {_table.Keyspace}.{_table.Name} WHERE ");
        bool first = true;
        for (int i = 0; i < keys.Length; i++)
        {
            var col = _table.PrimaryKey.Columns[i];
            if (keys[i].GetType() != GetManagedType(col.Type))
            {
                throw new Exception($"Expected type was {GetManagedType(col.Type)} and " +
                                    $"the parameter was of type {keys[i].GetType()}");
            }
            if (!first)
            {
                sb.Append($" AND ");
            }
            first = false;
            sb.Append($"{col.Name} = ?");
        }
        var statement = new SimpleStatement(sb.ToString(), keys.ToArray());
        var results = _context.Execute(statement);
        if (results == null)
        {
            return null;
        }
        var row = results.FirstOrDefault();
        return row?.GetValue<byte[]>($"{_table.Name}_data");
    }

    public async ValueTask<byte[]?> GetAsync(params object[] keys)
    {
        StringBuilder sb = new StringBuilder($"SELECT * FROM {_table.Keyspace}.{_table.Name} WHERE ");
        bool first = true;
        for (int i = 0; i < keys.Length; i++)
        {
            var col = _table.PrimaryKey.Columns[i];
            if (keys[i].GetType() != GetManagedType(col.Type))
            {
                throw new Exception($"Expected type was {GetManagedType(col.Type)} and " +
                                    $"the parameter was of type {keys[i].GetType()}");
            }
            if (!first)
            {
                sb.Append($" AND ");
            }
            first = false;
            sb.Append($"{col.Name} = ?");
        }
        var statement = new SimpleStatement(sb.ToString(), keys.ToArray());
        var results = await _context.ExecuteAsync(statement);
        if (results == null)
        {
            return null;
        }
        var row = results.FirstOrDefault();
        return row?.GetValue<byte[]>($"{_table.Name}_data");
    }

    public byte[]? GetBySecondaryIndex(string indexName, params object[] keys)
    {
        var sc = _table.SecondaryIndices.FirstOrDefault(x=>x.Name == indexName);
        if (sc != null)
        {
            StringBuilder sb = new StringBuilder($"SELECT * FROM {_table.Keyspace}.{_table.Name}_{sc.Name} WHERE ");
            bool first = true;
            for (int i = 0; i < keys.Length; i++)
            {
                var col = sc.Columns[i];
                if (keys[i].GetType() != GetManagedType(col.Type))
                {
                    throw new Exception($"Expected type was {GetManagedType(col.Type)} and " +
                                        $"the parameter was of type {keys[i].GetType()}");
                }
                if (!first)
                {
                    sb.Append($" AND ");
                }
                first = false;
                sb.Append($"{col.Name} = ?");
            }
            var statement = new SimpleStatement(sb.ToString(), keys.ToArray());
            var results = _context.Execute(statement);
            if (results == null)
            {
                return null;
            }
            var row = results.FirstOrDefault();
            if (row != null)
            {
                var statementInner = GenerateInnerStatement(row);
                var resultsInner = _context.Execute(statementInner);
                var rowInner = resultsInner.FirstOrDefault();
                return rowInner?.GetValue<byte[]>($"{_table.Name}_data");
            }
        }
        return null;
    }

    public async ValueTask<byte[]?> GetBySecondaryIndexAsync(string indexName, params object[] keys)
    {
        var sc = _table.SecondaryIndices.FirstOrDefault(x=>x.Name == indexName);
        if (sc != null)
        {
            StringBuilder sb = new StringBuilder($"SELECT * FROM {_table.Keyspace}.{_table.Name}_{sc.Name} WHERE ");
            bool first = true;
            for (int i = 0; i < keys.Length; i++)
            {
                var col = sc.Columns[i];
                if (keys[i].GetType() != GetManagedType(col.Type))
                {
                    throw new Exception($"Expected type was {GetManagedType(col.Type)} and " +
                                        $"the parameter was of type {keys[i].GetType()}");
                }
                if (!first)
                {
                    sb.Append($" AND ");
                }
                first = false;
                sb.Append($"{col.Name} = ?");
            }
            var statement = new SimpleStatement(sb.ToString(), keys.ToArray());
            var results = await _context.ExecuteAsync(statement);
            if (results == null)
            {
                return null;
            }
            var row = results.FirstOrDefault();
            if (row != null)
            {
                SimpleStatement statementInner = GenerateInnerStatement(row);
                var resultsInner = await _context.ExecuteAsync(statementInner);
                var rowInner = resultsInner.FirstOrDefault();
                return rowInner?.GetValue<byte[]>($"{_table.Name}_data");
            }
        }
        return null;
    }

    private SimpleStatement GenerateInnerStatement(Row row)
    {
        StringBuilder sb;
        bool first;
        List<object> primaryParameters = new();
        foreach (var c in _table.PrimaryKey.Columns)
        {
            primaryParameters.Add(row.GetValue(GetManagedType(c.Type), $"pk_{c.Name}"));
        }

        sb = new StringBuilder($"SELECT * FROM {_table.Keyspace}.{_table.Name} WHERE ");
        first = true;
        for (int i = 0; i < primaryParameters.Count; i++)
        {
            var col = _table.PrimaryKey.Columns[i];
            if (!first)
            {
                sb.Append($" AND ");
            }

            first = false;
            sb.Append($"{col.Name} = ?");
        }

        var statementInner = new SimpleStatement(sb.ToString(), primaryParameters.ToArray());
        return statementInner;
    }

    public IEnumerable<byte[]> Iterate(params object[] keys)
    {
        StringBuilder sb = new StringBuilder(
            $"SELECT * FROM {_table.Keyspace}.{_table.Name}");
        if (keys.Length > 0)
        {
            sb.Append(" WHERE ");
        }
        bool first = true;
        for (int i = 0; i < keys.Length; i++)
        {
            var col = _table.PrimaryKey.Columns[i];
            if (keys[i].GetType() != GetManagedType(col.Type))
            {
                throw new Exception($"Expected type was {GetManagedType(col.Type)} and " +
                                    $"the parameter was of type {keys[i].GetType()}");
            }
            if (!first)
            {
                sb.Append($" AND ");
            }
            first = false;
            sb.Append($"{col.Name} = ?");
        }
        var statement = new SimpleStatement(sb.ToString(), keys.ToArray());
        var results = _context.Execute(statement);
        if (results == null)
        {
            yield break;
        }
        foreach (var row in results)
        {
            yield return row.GetValue<byte[]>($"{_table.Name}_data");
        }
    }
    public async IAsyncEnumerable<byte[]> IterateAsync(params object[] keys)
    {
        StringBuilder sb = new StringBuilder(
            $"SELECT * FROM {_table.Keyspace}.{_table.Name}");
        if (keys.Length > 0)
        {
            sb.Append(" WHERE ");
        }
        bool first = true;
        for (int i = 0; i < keys.Length; i++)
        {
            var col = _table.PrimaryKey.Columns[i];
            if (keys[i].GetType() != GetManagedType(col.Type))
            {
                throw new Exception($"Expected type was {GetManagedType(col.Type)} and " +
                                    $"the parameter was of type {keys[i].GetType()}");
            }
            if (!first)
            {
                sb.Append($" AND ");
            }
            first = false;
            sb.Append($"{col.Name} = ?");
        }
        var statement = new SimpleStatement(sb.ToString(), keys.ToArray());
        var results = await _context.ExecuteAsync(statement);
        if (results == null)
        {
            yield break;
        }
        foreach (var row in results)
        {
            yield return row.GetValue<byte[]>($"{_table.Name}_data");
        }
    }

    public IEnumerable<byte[]> IterateBySecondaryIndex(string indexName, params object[] keys)
    {
        var sc = _table.SecondaryIndices.FirstOrDefault(x=>x.Name == indexName);
        if (sc != null)
        {
            StringBuilder sb = new StringBuilder($"SELECT * FROM {_table.Keyspace}.{_table.Name}_{sc.Name} WHERE ");
            bool first = true;
            for (int i = 0; i < keys.Length; i++)
            {
                var col = sc.Columns[i];
                if (keys[i].GetType() != GetManagedType(col.Type))
                {
                    throw new Exception($"Expected type was {GetManagedType(col.Type)} and " +
                                        $"the parameter was of type {keys[i].GetType()}");
                }
                if (!first)
                {
                    sb.Append($" AND ");
                }
                first = false;
                sb.Append($"{col.Name} = ?");
            }
            var statement = new SimpleStatement(sb.ToString(), keys.ToArray());
            var results = _context.Execute(statement);
            if (results == null)
            {
                yield break;
            }
            foreach (var row in results)
            {
                SimpleStatement statementInner = GenerateInnerStatement(row);
                var resultsInner = _context.Execute(statementInner);
                var rowInner = resultsInner.FirstOrDefault();
                yield return rowInner?.GetValue<byte[]>($"{_table.Name}_data");
            }
        }
    }

    public async IAsyncEnumerable<byte[]> IterateBySecondaryIndexAsync(string indexName, params object[] keys)
    {
        var sc = _table.SecondaryIndices.FirstOrDefault(x=>x.Name == indexName);
        if (sc != null)
        {
            StringBuilder sb = new StringBuilder($"SELECT * FROM {_table.Keyspace}.{_table.Name}_{sc.Name} WHERE ");
            bool first = true;
            for (int i = 0; i < keys.Length; i++)
            {
                var col = sc.Columns[i];
                if (keys[i].GetType() != GetManagedType(col.Type))
                {
                    throw new Exception($"Expected type was {GetManagedType(col.Type)} and " +
                                        $"the parameter was of type {keys[i].GetType()}");
                }
                if (!first)
                {
                    sb.Append($" AND ");
                }
                first = false;
                sb.Append($"{col.Name} = ?");
            }
            var statement = new SimpleStatement(sb.ToString(), keys.ToArray());
            var results = await _context.ExecuteAsync(statement);
            if (results == null)
            {
                yield break;
            }

            foreach (var row in results)
            {
                SimpleStatement statementInner = GenerateInnerStatement(row);
                var resultsInner = await _context.ExecuteAsync(statementInner);
                var rowInner = resultsInner.FirstOrDefault();
                yield return rowInner?.GetValue<byte[]>($"{_table.Name}_data");
            }
        }
    }
}
