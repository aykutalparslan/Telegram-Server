// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public sealed class LoginAttemptRepository : ILoginAttemptRepository
{
    private readonly IVolatileKVStore _byAuthKey;
    private readonly IVolatileKVStore _byPhoneHash;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public LoginAttemptRepository(IVolatileKVStore byAuthKey,
        IVolatileKVStore byPhoneHash)
    {
        _byAuthKey = byAuthKey;
        byAuthKey.SetSchema(new TableDefinition("ferrite", "login_attempts_by_auth_key",
            new KeyDefinition("pk",
                new DataColumn { Name = "auth_key_id", Type = DataType.Long })));
        _byPhoneHash = byPhoneHash;
        byPhoneHash.SetSchema(new TableDefinition("ferrite", "login_attempts_by_phone_hash",
            new KeyDefinition("pk",
                new DataColumn { Name = "phone", Type = DataType.String },
                new DataColumn { Name = "phone_code_hash", Type = DataType.String })));
    }

    private void DeleteIndexes(byte[] bytes)
    {
        var view = new TLDto.TLLoginAttempt(bytes, 0, bytes.Length).AsLoginAttempt();
        _byAuthKey.Delete(view.AuthKeyId);
        _byPhoneHash.Delete(Encoding.UTF8.GetString(view.Phone),
            Encoding.UTF8.GetString(view.PhoneCodeHash));
    }

    public async ValueTask PutAttemptAsync(TLDto.TLLoginAttempt attempt, TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        var view = attempt.AsLoginAttempt();
        long authKeyId = view.AuthKeyId;
        string phone = Encoding.UTF8.GetString(view.Phone);
        string phoneCodeHash = Encoding.UTF8.GetString(view.PhoneCodeHash);
        byte[] bytes = attempt.AsSpan().ToArray();

        await _gate.WaitAsync(cancellationToken);
        try
        {
            byte[]? oldByAuth = _byAuthKey.Get(authKeyId);
            if (oldByAuth != null)
            {
                DeleteIndexes(oldByAuth);
            }
            byte[]? oldByPhone = _byPhoneHash.Get(phone, phoneCodeHash);
            if (oldByPhone != null)
            {
                DeleteIndexes(oldByPhone);
            }
            _byAuthKey.Put(bytes, ttl, authKeyId);
            _byPhoneHash.Put(bytes, ttl, phone, phoneCodeHash);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<TLDto.TLLoginAttempt?> GetByAuthKeyAsync(long authKeyId,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            byte[]? bytes = _byAuthKey.Get(authKeyId);
            return bytes == null ? null : new TLDto.TLLoginAttempt(bytes, 0, bytes.Length);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<TLDto.TLLoginAttempt?> GetByPhoneHashAsync(string phone,
        string phoneCodeHash, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            byte[]? bytes = _byPhoneHash.Get(phone, phoneCodeHash);
            return bytes == null ? null : new TLDto.TLLoginAttempt(bytes, 0, bytes.Length);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<TLDto.TLLoginAttempt?> ConsumeByAuthKeyAsync(long authKeyId,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            byte[]? bytes = _byAuthKey.Get(authKeyId);
            if (bytes == null)
            {
                return null;
            }
            DeleteIndexes(bytes);
            return new TLDto.TLLoginAttempt(bytes, 0, bytes.Length);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask<TLDto.TLLoginAttempt?> ConsumeByPhoneHashAsync(string phone,
        string phoneCodeHash, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            byte[]? bytes = _byPhoneHash.Get(phone, phoneCodeHash);
            if (bytes == null)
            {
                return null;
            }
            DeleteIndexes(bytes);
            return new TLDto.TLLoginAttempt(bytes, 0, bytes.Length);
        }
        finally
        {
            _gate.Release();
        }
    }
}
