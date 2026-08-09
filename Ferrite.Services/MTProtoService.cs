// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System;
using System.Security.Cryptography;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL.mtproto;
using Ferrite.Utils;

namespace Ferrite.Services;

public class MTProtoService : IMTProtoService
{
    private readonly IAuthKeyRepository _authKeyRepository;
    private readonly IBoundAuthKeyRepository _boundAuthKeyRepository;
    private readonly IServerSaltRepository _serverSaltRepository;
    private readonly ITempAuthKeyRepository _tempAuthKeyRepository;

    private readonly IMTProtoTime _time;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISecretChatAuthKeyCleanup _secretChatCleanup;

    public MTProtoService(IMTProtoTime time, IUnitOfWork unitOfWork, IAuthKeyRepository authKeyRepository, IBoundAuthKeyRepository boundAuthKeyRepository, IServerSaltRepository serverSaltRepository, ITempAuthKeyRepository tempAuthKeyRepository,
        ISecretChatAuthKeyCleanup secretChatCleanup)
    {
        _authKeyRepository = authKeyRepository;
        _boundAuthKeyRepository = boundAuthKeyRepository;
        _serverSaltRepository = serverSaltRepository;
        _tempAuthKeyRepository = tempAuthKeyRepository;

        _time = time;
        _unitOfWork = unitOfWork;
        _secretChatCleanup = secretChatCleanup;
    }

    public IReadOnlyCollection<TLFutureSalt> GetServerSalts(long authKeyId, int count)
    {
        var serverSalts = _serverSaltRepository.GetServerSalts(authKeyId, count);
        if (serverSalts.Count != 0)
        {
            return serverSalts;
        }

        GenerateSalts(authKeyId);
        return _serverSaltRepository.GetServerSalts(authKeyId, count);
    }

    public async Task<IReadOnlyCollection<TLFutureSalt>> GetServerSaltsAsync(long authKeyId, int count)
    {
        var serverSalts = await _serverSaltRepository.GetServerSaltsAsync(authKeyId, count);
        if (serverSalts.Count != 0)
        {
            return serverSalts;
        }

        await GenerateSaltsAsync(authKeyId);
        return await _serverSaltRepository.GetServerSaltsAsync(authKeyId, count);
    }

    public bool PutServerSalt(long authKeyId, long serverSalt, int validForSeconds)
    {
        var validSince = checked((int)_time.GetUnixTimeInSeconds());
        using var salt = FutureSalt.Builder()
            .ValidSince(validSince)
            .ValidUntil(checked(validSince + validForSeconds))
            .Salt(serverSalt)
            .Build();
        return _serverSaltRepository.PutServerSalt(authKeyId, salt, validForSeconds) &&
               _unitOfWork.Save();
    }
    
    private void GenerateSalts(long authKeyId)
    {
        var time = _time.GetUnixTimeInSeconds();
        int offset = 0;
        byte[] saltBytes = new byte[8];
        for (int i = 0; i < 64; i++)
        {
            RandomNumberGenerator.Fill(saltBytes);
            long salt = BitConverter.ToInt64(saltBytes);
            using var futureSalt = FutureSalt.Builder()
                .ValidSince(checked((int)(time + offset)))
                .ValidUntil(checked((int)(time + offset + 3600)))
                .Salt(salt)
                .Build();
            _serverSaltRepository.PutServerSalt(authKeyId,
                futureSalt, offset + 3600);
            offset += 3600;
        }
        _unitOfWork.Save();
    }

    private async Task GenerateSaltsAsync(long authKeyId)
    {
        var time = _time.GetUnixTimeInSeconds();
        int offset = 0;
        byte[] saltBytes = new byte[8];
        for (int i = 0; i < 64; i++)
        {
            RandomNumberGenerator.Fill(saltBytes);
            long salt = BitConverter.ToInt64(saltBytes);
            using var futureSalt = FutureSalt.Builder()
                .ValidSince(checked((int)(time + offset)))
                .ValidUntil(checked((int)(time + offset + 3600)))
                .Salt(salt)
                .Build();
            _serverSaltRepository.PutServerSalt(authKeyId,
                futureSalt, offset + 3600);
            offset += 3600;
        }
        await _unitOfWork.SaveAsync();
    }

    public async Task<long> GetServerSaltValidityAsync(long authKeyId, long serverSalt)
    {
        long validSince = await _serverSaltRepository.GetServerSaltValidityAsync(authKeyId, serverSalt);
        if(validSince == 0)
        {
            var serverSalts = _serverSaltRepository.GetServerSaltsAsync(authKeyId, 64);
            if (serverSalts.Result.Count == 0)
            {
                _ = GenerateSaltsAsync(authKeyId);
            }
        }
        return validSince;
    }

    public async Task<bool> PutAuthKeyAsync(long authKeyId, byte[] authKey)
    {
        var result = _authKeyRepository.PutAuthKey(authKeyId, authKey);
        return result && await _unitOfWork.SaveAsync();
    }

    public bool PutAuthKey(long authKeyId, byte[] authKey)
    {
        var result = _authKeyRepository.PutAuthKey(authKeyId, authKey);
        return result && _unitOfWork.Save();
    }

    public byte[]? GetAuthKey(long authKeyId)
    {
        return _authKeyRepository.GetAuthKey(authKeyId);
    }

    public async Task<byte[]?> GetAuthKeyAsync(long authKeyId)
    {
        return await _authKeyRepository.GetAuthKeyAsync(authKeyId);
    }

    public bool PutTempAuthKey(long authKeyId, byte[] authKey, TimeSpan expiresIn)
    {
        var result = _tempAuthKeyRepository.PutTempAuthKey(authKeyId, authKey, expiresIn);
        return result && _unitOfWork.Save();
    }

    public async Task<bool> PutTempAuthKeyAsync(long authKeyId, byte[] authKey, TimeSpan expiresIn)
    {
        var result = _tempAuthKeyRepository.PutTempAuthKey(authKeyId, authKey, expiresIn);
        return result && await _unitOfWork.SaveAsync();
    }

    public byte[]? GetTempAuthKey(long authKeyId)
    {
        return _tempAuthKeyRepository.GetTempAuthKey(authKeyId);
    }

    public async Task<byte[]?> GetTempAuthKeyAsync(long authKeyId)
    {
        return await _tempAuthKeyRepository.GetTempAuthKeyAsync(authKeyId);
    }

    public async Task<bool> PutBoundAuthKey(long tempAuthKeyId, long authKeyId, TimeSpan expiresIn)
    {
        var result = _boundAuthKeyRepository.PutBoundAuthKey(tempAuthKeyId, authKeyId, expiresIn);
        return result && await _unitOfWork.SaveAsync();
    }

    public async ValueTask<long?> GetBoundAuthKeyAsync(long tempAuthKeyId)
    {
        return await _boundAuthKeyRepository.GetBoundAuthKeyAsync(tempAuthKeyId);
    }

    public long? GetBoundAuthKey(long tempAuthKeyId)
    {
        return _boundAuthKeyRepository.GetBoundAuthKey(tempAuthKeyId);
    }

    public async Task<bool> DestroyAuthKeyAsync(long authKeyId)
    {
        if (await _authKeyRepository.GetAuthKeyAsync(authKeyId) == null)
        {
            return false;
        }

        await _secretChatCleanup.CleanupAsync(authKeyId);
        var success = _authKeyRepository.DeleteAuthKey(authKeyId);
        return success && await _unitOfWork.SaveAsync();
    }

    public async Task<KeyStatus> GetKeyStatus(long keyId)
    {
        if (await _authKeyRepository.GetAuthKeyAsync(keyId) != null)
        {
            return KeyStatus.Perm;
        }

        if (await _tempAuthKeyRepository.GetTempAuthKeyAsync(keyId) != null &&
            await _boundAuthKeyRepository.GetBoundAuthKeyAsync(keyId) != null)
        {
            return KeyStatus.TempBound;
        }

        return KeyStatus.TempUnbound;
    }
}
