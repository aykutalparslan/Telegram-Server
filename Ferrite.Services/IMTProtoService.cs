// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System;
using Ferrite.TL.mtproto;
using Nest;

namespace Ferrite.Services;

public interface IMTProtoService
{
    public IReadOnlyCollection<TLFutureSalt> GetServerSalts(long authKeyId, int count);
    public Task<IReadOnlyCollection<TLFutureSalt>> GetServerSaltsAsync(long authKeyId, int count);
    public bool PutServerSalt(long authKeyId, long serverSalt, int validForSeconds);
    public Task<long> GetServerSaltValidityAsync(long authKeyId, long serverSalt);
    public Task<bool> PutAuthKeyAsync(long authKeyId, byte[] authKey);
    public bool PutAuthKey(long authKeyId, byte[] authKey);
    public byte[]? GetAuthKey(long authKeyId);
    public Task<byte[]?> GetAuthKeyAsync(long authKeyId);
    public bool PutTempAuthKey(long authKeyId, byte[] authKey, TimeSpan expiresIn);
    public Task<bool> PutTempAuthKeyAsync(long authKeyId, byte[] authKey, TimeSpan expiresIn);
    public byte[]? GetTempAuthKey(long authKeyId);
    public Task<byte[]?> GetTempAuthKeyAsync(long authKeyId);
    public Task<bool> PutBoundAuthKey(long tempAuthKeyId, long authKeyId, TimeSpan expiresIn);
    public ValueTask<long?> GetBoundAuthKeyAsync(long tempAuthKeyId);
    public long? GetBoundAuthKey(long tempAuthKeyId);
    public Task<bool> DestroyAuthKeyAsync(long authKeyId);
    public Task<KeyStatus> GetKeyStatus(long keyId);
}
