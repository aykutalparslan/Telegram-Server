// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System;
using Ferrite.TL;
using Ferrite.TL.baseLayer.auth;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services;

public interface IAuthService
{
    public ValueTask<bool> SaveAppInfo(TLAppInfo info);
    public ValueTask<bool> IsAuthorized(long authKeyId);
    public ValueTask<TLAuthorization> SignUp(long authKeyId, TLBytes q);
    public ValueTask<TLAuthorization> SignIn(long authKeyId, TLBytes q);
    public ValueTask<TLBool> BindTempAuthKey(long sessionId, TLBytes q);
    public ValueTask<TLLoginToken> ExportLoginToken(long authKeyId, long sessionId, TLBytes q);
}
