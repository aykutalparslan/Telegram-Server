// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using DotNext;

namespace Ferrite.Data.Repositories;

public interface IKVStore
{
    void SetSchema(TableDefinition table);
    public bool Put(byte[] data, params object[] keys);
    public bool Delete(params object[] keys);
    public ValueTask<bool> DeleteAsync(params object[] keys);
    public bool DeleteBySecondaryIndex(string indexName, params object[] keys);
    public ValueTask<bool> DeleteBySecondaryIndexAsync(string indexName, params object[] keys);
    public byte[]? Get(params object[] keys);
    public ValueTask<byte[]?> GetAsync(params object[] keys);
    public byte[]? GetBySecondaryIndex(string indexName, params object[] keys);
    public ValueTask<byte[]?> GetBySecondaryIndexAsync(string indexName, params object[] keys);
    public IEnumerable<byte[]> Iterate(params object[] keys);
    public IAsyncEnumerable<byte[]> IterateAsync(params object[] keys);
    public IEnumerable<byte[]> IterateBySecondaryIndex(string indexName, params object[] keys);
    public IAsyncEnumerable<byte[]> IterateBySecondaryIndexAsync(string indexName, params object[] keys);
}