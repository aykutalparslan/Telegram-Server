// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public interface IAppInfoRepository
{
    public bool PutAppInfo(TLAppInfo appInfo);
    public TLAppInfo? GetAppInfo(long authKeyId);
    public TLAppInfo? GetAppInfoByAppHash(long hash);
    public long? GetAuthKeyIdByAppHash(long hash);
}