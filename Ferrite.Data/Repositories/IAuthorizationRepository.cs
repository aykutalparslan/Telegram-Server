// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public interface IAuthorizationRepository
{
    public bool PutAuthorization(TLAuthInfo info);
    public TLAuthInfo? GetAuthorization(long authKeyId);
    public ValueTask<TLAuthInfo?> GetAuthorizationAsync(long authKeyId);
    public IReadOnlyList<TLAuthInfo> GetAuthorizations(string phone);
    public ValueTask<IReadOnlyList<TLAuthInfo>> GetAuthorizationsAsync(string phone);
    public bool DeleteAuthorization(long authKeyId);
    public bool PutExportedAuthorization(TLExportedAuthInfo exportedInfo);
    public TLExportedAuthInfo? GetExportedAuthorization(long userId, byte[] data);
    public ValueTask<TLExportedAuthInfo?> GetExportedAuthorizationAsync(long userId, byte[] data);
}