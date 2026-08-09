// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using System.Text.Unicode;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public class DeviceInfoRepository : IDeviceInfoRepository
{
    private readonly IKVStore _store;
    private readonly IKVStore _storeUsers;
    public DeviceInfoRepository(IKVStore store, IKVStore storeUsers)
    {
        _store = store;
        _store.SetSchema(new TableDefinition("ferrite", "devices",
            new KeyDefinition("pk",
                new DataColumn { Name = "auth_key_id", Type = DataType.Long },
                new DataColumn { Name = "app_token", Type = DataType.String })));
        _storeUsers = storeUsers;
        _storeUsers.SetSchema(new TableDefinition("ferrite", "device_users",
            new KeyDefinition("pk",
                new DataColumn { Name = "auth_key_id", Type = DataType.Long },
                new DataColumn { Name = "user_id", Type = DataType.Long },
                new DataColumn { Name = "app_token", Type = DataType.String })));
    }
    public bool PutDeviceInfo(long authKeyId, TLDeviceInfo deviceInfo)
    {
        var infoBytes = deviceInfo.AsSpan().ToArray();
        var token = Encoding.UTF8.GetString(deviceInfo.AsDeviceInfo().Token);
        _store.Put(infoBytes, authKeyId, token);
        var userIds = deviceInfo.AsDeviceInfo().OtherUids;
        for(int i = 0; i < userIds.Count; i++)
        {
            var userId = userIds[i];
            using TLDeviceUser user = DeviceUser.Builder()
                .UserId(userId)
                .Token(deviceInfo.AsDeviceInfo().Token)
                .Build();
            _storeUsers.Put(user.AsSpan().ToArray(), 
                authKeyId, userId);
        }

        return true;
    }

    public TLDeviceInfo? GetDeviceInfo(long authKeyId)
    {
        var infoBytes = _store.Get(authKeyId);
        if (infoBytes == null) return null;
        return new TLDeviceInfo(infoBytes, 0 ,infoBytes.Length);
    }

    public bool DeleteDeviceInfo(long authKeyId, string token, ICollection<long> otherUserIds)
    {
        _store.Delete(authKeyId, token);
        foreach (var userId in otherUserIds)
        {
            _storeUsers.Delete(authKeyId, userId);
        }

        return true;
    }
}
