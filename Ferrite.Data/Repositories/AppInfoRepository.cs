// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public class AppInfoRepository : IAppInfoRepository
{
    private readonly IKVStore _store;

    public AppInfoRepository(IKVStore store)
    {
        _store = store;
        _store.SetSchema(new TableDefinition("ferrite", "app_infos",
            new KeyDefinition("pk",
                new DataColumn { Name = "auth_key_id", Type = DataType.Long },
                new DataColumn { Name = "hash", Type = DataType.Long }),
            new KeyDefinition("by_hash",
                new DataColumn { Name = "hash", Type = DataType.Long })));
    }
    public bool PutAppInfo(TLAppInfo appInfo)
    {
        var info = appInfo.AsAppInfo();
        _store.Put(appInfo.AsSpan().ToArray(), info.AuthKeyId, info.Hash);
        return true;
    }

    public TLAppInfo? GetAppInfo(long authKeyId)
    {
        var appInfoBytes = _store.Get(authKeyId);
        return appInfoBytes != null ? new TLAppInfo(appInfoBytes, 0, appInfoBytes.Length) : null;
    }

    public TLAppInfo? GetAppInfoByAppHash(long hash)
    {
        var appInfoBytes = _store.GetBySecondaryIndex("by_hash", hash);
        return appInfoBytes != null ? new TLAppInfo(appInfoBytes, 0, appInfoBytes.Length) : null;
    }

    public long? GetAuthKeyIdByAppHash(long hash)
    {
        var appInfoBytes = _store.GetBySecondaryIndex("by_hash", hash);
        if (appInfoBytes == null) return null;
        return ((AppInfo)appInfoBytes.AsSpan()).AuthKeyId;
    }
}
