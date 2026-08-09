// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Data.Repositories;

public interface IBoundAuthKeyRepository
{
    public bool PutBoundAuthKey(long tempAuthKeyId, long authKeyId, TimeSpan expiresIn);
    public long? GetBoundAuthKey(long tempAuthKeyId);
    public ValueTask<long?> GetBoundAuthKeyAsync(long tempAuthKeyId);
    public IReadOnlyList<long> GetTempAuthKeys(long authKeyId);
}