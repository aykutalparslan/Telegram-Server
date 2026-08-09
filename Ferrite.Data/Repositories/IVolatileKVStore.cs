// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Data.Repositories;

/// <summary>
/// Keeps the data in RAM only and never saves it in persistent storage.
/// </summary>
public interface IVolatileKVStore
{
    /// <summary>
    /// Sets the schema.
    /// </summary>
    /// <param name="table"></param>
    void SetSchema(TableDefinition table);
    /// <summary>
    /// 
    /// </summary>
    /// <param name="value"></param>
    /// <param name="Ttl">Time-to-live in milliseconds. null is treated as infinity.</param>
    /// <param name="keys"></param>
    public void Put(byte[] value, TimeSpan? ttl = null, params object[] keys);
    public void UpdateTtl(TimeSpan? ttl = null, params object[] keys);
    public bool ListAdd(long score, byte[] value, TimeSpan? ttl = null, params object[] keys);
    public bool ListDelete(byte[] value, params object[] keys);
    public bool ListDeleteByScore(long score, params object[] keys);
    public IList<byte[]> ListGet(params object[] keys);
    public void Delete(params object[] keys);
    public bool Exists(params object[] keys);
    public byte[]? Get(params object[] keys);
    public ValueTask<byte[]?> GetAsync(params object[] keys);
}