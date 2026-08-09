// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using DotNext;

namespace Ferrite.Data.Repositories;

public interface IKVStore
{
    /// <summary>
    /// Sets the schema.
    /// </summary>
    /// <param name="table"></param>
    void SetSchema(TableDefinition table);
    /// <summary>
    /// Enqueues a put operation.
    /// </summary>
    /// <param name="data">The data to be written.</param>
    /// <param name="keys">Fields of the primary key with the right order.</param>
    /// <returns>True if the operation is successful.</returns>
    public bool Put(byte[] data, params object[] keys);
    /// <summary>
    /// Enqueues a delete operation.
    /// </summary>
    /// <param name="keys">Fields of the primary key with the right order.</param>
    /// <returns></returns>
    public bool Delete(params object[] keys);
    /// <summary>
    /// Enqueues a delete operation.
    /// </summary>
    /// <param name="keys">Fields of the primary key with the right order.</param>
    /// <returns></returns>
    public ValueTask<bool> DeleteAsync(params object[] keys);
    /// <summary>
    /// Enqueues a delete operation.
    /// </summary>
    /// <param name="indexName">Name of the secondary index to be used with the operation.</param>
    /// <param name="keys">Fields of the secondary key with the right order.</param>
    /// <returns></returns>
    public bool DeleteBySecondaryIndex(string indexName, params object[] keys);
    /// <summary>
    /// Enqueues a delete operation.
    /// </summary>
    /// <param name="indexName">Name of the secondary index to be used with the operation.</param>
    /// <param name="keys">Fields of the secondary key with the right order.</param>
    /// <returns></returns>
    public ValueTask<bool> DeleteBySecondaryIndexAsync(string indexName, params object[] keys);
    /// <summary>
    /// Gets a single value for the given key.
    /// </summary>
    /// <param name="keys">Fields of the primary key with the right order.</param>
    /// <returns>The value stored</returns>
    public byte[]? Get(params object[] keys);
    /// <summary>
    /// Gets a single value for the given key.
    /// </summary>
    /// <param name="keys">Fields of the primary key with the right order.</param>
    /// <returns>The value stored</returns>
    public ValueTask<byte[]?> GetAsync(params object[] keys);
    /// <summary>
    /// Gets a single value for the given key.
    /// </summary>
    /// <param name="indexName">Name of the secondary index to be used with the operation.</param>
    /// <param name="keys">Fields of the secondary key with the right order.</param>
    /// <returns>The value stored</returns>
    public byte[]? GetBySecondaryIndex(string indexName, params object[] keys);
    /// <summary>
    /// Gets a single value for the given key.
    /// </summary>
    /// <param name="indexName">Name of the secondary index to be used with the operation.</param>
    /// <param name="keys">Fields of the secondary key with the right order.</param>
    /// <returns>The value stored</returns>
    public ValueTask<byte[]?> GetBySecondaryIndexAsync(string indexName, params object[] keys);
    /// <summary>
    /// Gets the matching values for the given key.
    /// </summary>
    /// <param name="keys">Fields of the primary key with the right order.</param>
    /// <returns>Enumerator for the matching values.</returns>
    public IEnumerable<byte[]> Iterate(params object[] keys);
    /// <summary>
    /// Gets the matching values for the given key.
    /// </summary>
    /// <param name="keys">Fields of the primary key with the right order.</param>
    /// <returns>Enumerator for the matching values.</returns>
    public IAsyncEnumerable<byte[]> IterateAsync(params object[] keys);
    public IEnumerable<byte[]> IterateBySecondaryIndex(string indexName, params object[] keys);
    public IAsyncEnumerable<byte[]> IterateBySecondaryIndexAsync(string indexName, params object[] keys);
}