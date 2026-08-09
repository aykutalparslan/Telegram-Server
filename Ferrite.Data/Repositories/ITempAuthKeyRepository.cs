// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Data.Repositories;

public interface ITempAuthKeyRepository
{
    public bool PutTempAuthKey(long tempAuthKeyId, byte[] tempAuthKey, TimeSpan? expiresIn = null);
    public bool DeleteTempAuthKey(long tempAuthKeyId);
    public byte[]? GetTempAuthKey(long tempAuthKeyId);
    public ValueTask<byte[]?> GetTempAuthKeyAsync(long tempAuthKeyId);
}