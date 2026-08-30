// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using RocksDbSharp;

namespace Ferrite.Data.Repositories;

public class RocksDBContext : IDisposable
{
    private readonly RocksDb _db;
    private readonly ColumnFamilyHandle _cf;
    public RocksDBContext()
    {
        _db = RocksDb.Open( new DbOptions().SetCreateIfMissing(true), "ferrite", new ColumnFamilies());
        _cf = _db.GetDefaultColumnFamily();
    }
    public RocksDBContext(string path)
    {
        _db = RocksDb.Open( new DbOptions().SetCreateIfMissing(true), path, new ColumnFamilies());
        _cf = _db.GetDefaultColumnFamily();
    }
    public void Put(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
    {
        _db.Put(key, value, _cf);
    }
    public byte[] Get(ReadOnlySpan<byte> key)
    {
        return _db.Get(key, _cf);
    }
    public void Delete(ReadOnlySpan<byte> key)
    {
        _db.Remove(key, _cf);
    }
    public void DeleteWithPrefix(byte[] key)
    {
        _db.RemoveWithPrefix(key, _cf);
    }
    public IEnumerable<byte[]> Iterate(byte[] key)
    {
        var iter = _db.NewIterator(_cf);
        iter.Seek(key);
        while(iter.Valid())
        {
            var iterKey = iter.Key();
            if (iterKey.Length < key.Length) yield break;
            if (!key.AsSpan().SequenceEqual(iterKey.AsSpan(0, key.Length)))
            {
                yield break;
            }
            yield return iter.Value();
            iter.Next();
        }
    }
    public IEnumerable<byte[]> IterateKeys(byte[] key)
    {
        var iter = _db.NewIterator(_cf);
        iter.Seek(key);
        while(iter.Valid())
        {
            var iterKey = iter.Key();
            if (iterKey.Length < key.Length) yield break;
            if (!key.AsSpan().SequenceEqual(iterKey.AsSpan(0, key.Length)))
            {
                yield break;
            }
            yield return iterKey;
            iter.Next();
        }
    }

    public void Dispose()
    {
        _db.Dispose();
    }
}