// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using RocksDbSharp;

namespace Ferrite.Data.Repositories;

public static class RocksDbExtensions
{
    private static WriteOptions DefaultWriteOptions { get; } = new WriteOptions();
    public static void RemoveWithPrefix(this RocksDb db, byte[] prefix, ColumnFamilyHandle cf = null, WriteOptions writeOptions = null)
    {
        var end = new byte[prefix.Length];
        prefix.CopyTo(end, 0);
        int idx = end.Length - 1;
        while (++end[idx] == 0 && idx > 0)
        {
            idx--;
        }
        RocksDbSharp.Native.Instance.rocksdb_delete_range_cf(db.Handle,
            (writeOptions ?? DefaultWriteOptions).Handle,
            (cf ?? db.GetDefaultColumnFamily()).Handle,
            prefix, (nuint)prefix.Length,
            end, (nuint)end.Length);
    }
}