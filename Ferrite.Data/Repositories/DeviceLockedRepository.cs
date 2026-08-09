// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Data.Repositories;

using Ferrite.TL;
using Ferrite.TL.baseLayer.dto;

public class DeviceLockedRepository : IDeviceLockedRepository
{
    private readonly IVolatileKVStore _store;
    public DeviceLockedRepository(IVolatileKVStore store)
    {
        _store = store;
        store.SetSchema(new TableDefinition("ferrite", "locked_devices_tl1",
            new KeyDefinition("pk",
                new DataColumn { Name = "auth_key_id", Type = DataType.Long })));
    }
    public bool PutDeviceLocked(long authKeyId, TimeSpan period)
    {
        var lockedUntil = DateTimeOffset.Now.Add(period).ToUnixTimeMilliseconds();
        using var row = DeviceLockState.Builder().LockedUntil(lockedUntil).Build();
        _store.Put(row.ToReadOnlySpan().ToArray(), period, authKeyId);
        return true;
    }

    public TimeSpan? GetDeviceLocked(long authKeyId)
    {
        var status = _store.Get(authKeyId);
        if (status != null)
        {
            var value = new TLBytes(status, 0, status.Length);
            if (value.Constructor != Constructors.baseLayer_DeviceLockState)
                throw new InvalidDataException("Device-lock codec/version mismatch.");
            long lockedUntil = ((TLDeviceLockState)value).AsDeviceLockState().LockedUntil;
            var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            return TimeSpan.FromMilliseconds(lockedUntil - now);
        }

        return null;
    }
}
