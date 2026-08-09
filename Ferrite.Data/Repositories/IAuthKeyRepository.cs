// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Data.Repositories;

public interface IAuthKeyRepository
{
    public bool PutAuthKey(long authKeyId, byte[] authKey);
    public byte[]? GetAuthKey(long authKeyId);
    public ValueTask<byte[]?> GetAuthKeyAsync(long authKeyId);
    public bool DeleteAuthKey(long authKeyId);
}