// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public sealed class AccountPasswordRepository : IAccountPasswordRepository
{
    private const int StripeCount = 256;

    private readonly IKVStore _passwordStates;
    private readonly IKVStore _resetStates;
    private readonly IVolatileKVStore _srpChallenges;
    private readonly IVolatileKVStore _temporaryPasswords;
    private readonly Func<ValueTask<bool>> _flush;
    private readonly SemaphoreSlim[] _userGates = CreateGates();
    private readonly SemaphoreSlim[] _srpGates = CreateGates();

    public AccountPasswordRepository(IKVStore passwordStates, IKVStore resetStates,
        IVolatileKVStore srpChallenges, IVolatileKVStore temporaryPasswords,
        Func<ValueTask<bool>>? flush = null)
    {
        _passwordStates = passwordStates;
        passwordStates.SetSchema(new TableDefinition("ferrite", "account_password_states",
            new KeyDefinition("pk",
                new DataColumn { Name = "user_id", Type = DataType.Long })));
        _resetStates = resetStates;
        resetStates.SetSchema(new TableDefinition("ferrite", "password_reset_states",
            new KeyDefinition("pk",
                new DataColumn { Name = "user_id", Type = DataType.Long })));
        _srpChallenges = srpChallenges;
        srpChallenges.SetSchema(new TableDefinition("ferrite", "password_srp_challenges",
            new KeyDefinition("pk",
                new DataColumn { Name = "srp_id", Type = DataType.Long })));
        _temporaryPasswords = temporaryPasswords;
        temporaryPasswords.SetSchema(new TableDefinition("ferrite", "temporary_passwords",
            new KeyDefinition("pk",
                new DataColumn { Name = "user_id", Type = DataType.Long })));
        _flush = flush ?? (() => ValueTask.FromResult(true));
    }

    private static SemaphoreSlim[] CreateGates() =>
        Enumerable.Range(0, StripeCount).Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    private static SemaphoreSlim Gate(SemaphoreSlim[] gates, long key) =>
        gates[(int)(unchecked((ulong)key) % (uint)gates.Length)];

    private async ValueTask FlushAsync(string operation)
    {
        if (!await _flush())
        {
            throw new IOException($"Failed to persist {operation}.");
        }
    }

    public async ValueTask<TLDto.TLAccountPasswordState?> GetPasswordStateAsync(long userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        byte[]? bytes = await _passwordStates.GetAsync(userId);
        return bytes == null ? null : new TLDto.TLAccountPasswordState(bytes, 0, bytes.Length);
    }

    public async ValueTask PutPasswordStateAsync(TLDto.TLAccountPasswordState state,
        CancellationToken cancellationToken = default)
    {
        long userId = state.AsAccountPasswordState().UserId;
        SemaphoreSlim gate = Gate(_userGates, userId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            _passwordStates.Put(state.AsSpan().ToArray(), userId);
            await FlushAsync("account password state");
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<bool> DeletePasswordStateAsync(long userId,
        CancellationToken cancellationToken = default)
    {
        SemaphoreSlim gate = Gate(_userGates, userId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            bool deleted = _passwordStates.Delete(userId);
            await FlushAsync("account password state deletion");
            return deleted;
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask PutSrpChallengeAsync(TLDto.TLPasswordSrpChallenge challenge,
        TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        long srpId = challenge.AsPasswordSrpChallenge().SrpId;
        SemaphoreSlim gate = Gate(_srpGates, srpId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            _srpChallenges.Delete(srpId);
            _srpChallenges.Put(challenge.AsSpan().ToArray(), ttl, srpId);
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<TLDto.TLPasswordSrpChallenge?> GetSrpChallengeAsync(long srpId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        byte[]? bytes = await _srpChallenges.GetAsync(srpId);
        return bytes == null ? null : new TLDto.TLPasswordSrpChallenge(bytes, 0, bytes.Length);
    }

    public async ValueTask<TLDto.TLPasswordSrpChallenge?> ConsumeSrpChallengeAsync(long srpId,
        CancellationToken cancellationToken = default)
    {
        SemaphoreSlim gate = Gate(_srpGates, srpId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            byte[]? bytes = await _srpChallenges.GetAsync(srpId);
            if (bytes == null)
            {
                return null;
            }
            _srpChallenges.Delete(srpId);
            return new TLDto.TLPasswordSrpChallenge(bytes, 0, bytes.Length);
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask PutTemporaryPasswordAsync(TLDto.TLTemporaryPasswordState password,
        TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        long userId = password.AsTemporaryPasswordState().UserId;
        SemaphoreSlim gate = Gate(_userGates, userId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            _temporaryPasswords.Delete(userId);
            _temporaryPasswords.Put(password.AsSpan().ToArray(), ttl, userId);
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<TLDto.TLTemporaryPasswordState?> GetTemporaryPasswordAsync(
        long userId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        byte[]? bytes = await _temporaryPasswords.GetAsync(userId);
        return bytes == null
            ? null
            : new TLDto.TLTemporaryPasswordState(bytes, 0, bytes.Length);
    }

    public async ValueTask<TLDto.TLTemporaryPasswordState?> ConsumeTemporaryPasswordAsync(
        long userId, CancellationToken cancellationToken = default)
    {
        SemaphoreSlim gate = Gate(_userGates, userId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            byte[]? bytes = await _temporaryPasswords.GetAsync(userId);
            if (bytes == null)
            {
                return null;
            }
            _temporaryPasswords.Delete(userId);
            return new TLDto.TLTemporaryPasswordState(bytes, 0, bytes.Length);
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<TLDto.TLPasswordResetState?> GetResetStateAsync(long userId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        byte[]? bytes = await _resetStates.GetAsync(userId);
        return bytes == null ? null : new TLDto.TLPasswordResetState(bytes, 0, bytes.Length);
    }

    public async ValueTask PutResetStateAsync(TLDto.TLPasswordResetState state,
        CancellationToken cancellationToken = default)
    {
        long userId = state.AsPasswordResetState().UserId;
        SemaphoreSlim gate = Gate(_userGates, userId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            _resetStates.Put(state.AsSpan().ToArray(), userId);
            await FlushAsync("password reset state");
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<bool> DeleteResetStateAsync(long userId,
        CancellationToken cancellationToken = default)
    {
        SemaphoreSlim gate = Gate(_userGates, userId);
        await gate.WaitAsync(cancellationToken);
        try
        {
            bool deleted = _resetStates.Delete(userId);
            await FlushAsync("password reset state deletion");
            return deleted;
        }
        finally
        {
            gate.Release();
        }
    }
}
