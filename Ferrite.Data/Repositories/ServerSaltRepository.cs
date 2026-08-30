// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Security.Cryptography;
using Ferrite.TL;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.mtproto;
using Ferrite.Utils;

namespace Ferrite.Data.Repositories;

public class ServerSaltRepository : IServerSaltRepository
{
    private readonly IVolatileKVStore _store;
    private readonly IVolatileKVStore _validityStore;
    public ServerSaltRepository(IVolatileKVStore store, IVolatileKVStore validityStore)
    {
        _store = store;
        store.SetSchema(new TableDefinition("ferrite", "server_salts_tl1",
            new KeyDefinition("pk",
                new DataColumn { Name = "auth_key_id", Type = DataType.Long })));
        _validityStore = validityStore;
        _validityStore.SetSchema(new TableDefinition("ferrite", "server_salt_validity_tl1",
            new KeyDefinition("pk",
                new DataColumn { Name = "auth_key_id", Type = DataType.Long },
                new DataColumn { Name = "server_salt", Type = DataType.Long })));
    }
    public bool PutServerSalt(long authKeyId, TLFutureSalt salt, int TTL)
    {
        var value = salt.AsFutureSalt();
        var saltBytes = salt.AsSpan().ToArray();
        var expire = TimeSpan.FromSeconds(TTL);
        _store.ListAdd(DateTimeOffset.Now.AddSeconds(TTL).ToUnixTimeMilliseconds(),
            saltBytes, expire, authKeyId);
        using var validity = ServerSaltValidity.Builder()
            .ValidUntil(value.ValidUntil)
            .Build();
        _validityStore.Put(validity.ToReadOnlySpan().ToArray(), expire,
            authKeyId, value.Salt);
        return true;
    }

    public IReadOnlyCollection<TLFutureSalt> GetServerSalts(long authKeyId, int count)
    {
        count = Math.Min(count, 64);
        List<TLFutureSalt> salts = new();
        var existing = _store.ListGet(authKeyId);
        foreach (var b in existing)
        {
            var bytes = new TLBytes(b, 0, b.Length);
            if (bytes.Constructor != Constructors.mtproto_FutureSalt)
                throw new InvalidDataException("Server-salt codec/version mismatch.");
            var salt = (TLFutureSalt)bytes;
            if (salt.AsFutureSalt().ValidUntil >= DateTimeOffset.Now.ToUnixTimeSeconds())
            {
                salts.Add(salt);
            }
            else
            {
                _store.ListDelete(b, authKeyId);
            }
        }

        if (salts.Count > count)
        {
            salts = salts.OrderBy(s => s.AsFutureSalt().ValidSince).Take(count).ToList();
        }
        else if (salts.Count == 0)
        {
            Span<byte> randomBytes = stackalloc byte[8];
            int validSince = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
            for (int i = 0; i < count; i++)
            {
                RandomNumberGenerator.Fill(randomBytes);
                long salt = BitConverter.ToInt64(randomBytes);
                var built = FutureSalt.Builder()
                    .ValidSince(validSince)
                    .ValidUntil(validSince + 1800)
                    .Salt(salt)
                    .Build();
                TLFutureSalt s = built;
                validSince += 1800;
                salts.Add(s);
                var saltBytes = s.AsSpan().ToArray();
                int ttl = validSince - (int)DateTimeOffset.Now.ToUnixTimeSeconds() + 1800;
                var expire = TimeSpan.FromSeconds(ttl);
                _store.ListAdd((long)(validSince + 1800) * 1000, saltBytes,
                    expire, authKeyId);
                using var validity = ServerSaltValidity.Builder()
                    .ValidUntil(validSince + 1800)
                    .Build();
                _validityStore.Put(validity.ToReadOnlySpan().ToArray(), expire,
                    authKeyId, salt);
            }
        }
        return salts;
    }

    public ValueTask<IReadOnlyCollection<TLFutureSalt>> GetServerSaltsAsync(long authKeyId, int count)
    {
        return new ValueTask<IReadOnlyCollection<TLFutureSalt>>(GetServerSalts(authKeyId, count));
    }

    public long GetServerSaltValidity(long authKeyId, long serverSalt)
    {
        byte[]? bytes = _validityStore.Get(authKeyId, serverSalt);
        if (bytes is not { Length: > 0 }) return 0;
        var value = new TLBytes(bytes, 0, bytes.Length);
        if (value.Constructor != Constructors.baseLayer_ServerSaltValidity)
            throw new InvalidDataException("Server-salt validity codec/version mismatch.");
        return ((TLServerSaltValidity)value).AsServerSaltValidity().ValidUntil;
    }

    public ValueTask<long> GetServerSaltValidityAsync(long authKeyId, long serverSalt)
    {
        return new ValueTask<long>(GetServerSaltValidity(authKeyId, serverSalt));
    }
}
