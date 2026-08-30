// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Data.Repositories;

public interface IVolatileKVStore
{
    void SetSchema(TableDefinition table);
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