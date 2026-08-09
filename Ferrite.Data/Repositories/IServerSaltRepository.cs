// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Data.Repositories;

using Ferrite.TL.mtproto;

public interface IServerSaltRepository
{
    public bool PutServerSalt(long authKeyId, TLFutureSalt salt, int TTL);
    public IReadOnlyCollection<TLFutureSalt> GetServerSalts(long authKeyId, int count);
    public ValueTask<IReadOnlyCollection<TLFutureSalt>> GetServerSaltsAsync(long authKeyId, int count);
    public long GetServerSaltValidity(long authKeyId, long serverSalt);
    public ValueTask<long> GetServerSaltValidityAsync(long authKeyId, long serverSalt);
}
