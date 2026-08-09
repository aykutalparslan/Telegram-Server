// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Data.Repositories;

public interface IKVStoreFactory
{
    IKVStore Create(IWriteBatchAccessor writeBatches);
}

public interface IVolatileKVStoreFactory
{
    IVolatileKVStore Create();
}

public sealed class RocksDbKVStoreFactory : IKVStoreFactory, IDisposable
{
    private readonly RocksDBContext _context;

    public RocksDbKVStoreFactory(string path)
    {
        _context = new RocksDBContext(path);
    }

    public IKVStore Create(IWriteBatchAccessor writeBatches) =>
        new RocksDBKVStore(_context);

    public void Dispose() => _context.Dispose();
}

public sealed class CassandraKVStoreFactory : IKVStoreFactory, IDisposable
{
    private readonly CassandraContext _context;
    public ICassandraContext Context => _context;

    public CassandraKVStoreFactory(string keyspace, int port, params string[] hosts)
    {
        _context = new CassandraContext(keyspace, port, hosts);
    }

    public IKVStore Create(IWriteBatchAccessor writeBatches) =>
        new CassandraKVStore(_context, writeBatches);

    public void Dispose() => _context.Dispose();
}

public sealed class InMemoryStoreFactory : IVolatileKVStoreFactory
{
    public IVolatileKVStore Create() => new InMemoryStore();
}

public sealed class RedisDataStoreFactory : IVolatileKVStoreFactory, IDisposable
{
    private readonly StackExchange.Redis.ConnectionMultiplexer _connection;

    public RedisDataStoreFactory(string configuration)
    {
        _connection = StackExchange.Redis.ConnectionMultiplexer.Connect(configuration);
    }

    public IVolatileKVStore Create() => new RedisDataStore(_connection);

    public void Dispose() => _connection.Dispose();
}
