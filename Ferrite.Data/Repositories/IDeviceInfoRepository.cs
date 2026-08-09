// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public interface IDeviceInfoRepository
{
    public bool PutDeviceInfo(long authKeyId, TLDeviceInfo deviceInfo);
    public TLDeviceInfo? GetDeviceInfo(long authKeyId);
    public bool DeleteDeviceInfo(long authKeyId, string token, ICollection<long> otherUserIds);
}