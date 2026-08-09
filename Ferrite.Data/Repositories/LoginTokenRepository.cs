// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public sealed class LoginTokenRepository : ILoginTokenRepository
{
    private const int StripeCount = 256;

    private readonly IVolatileKVStore _qrTokens;
    private readonly IVolatileKVStore _webTokens;
    private readonly SemaphoreSlim[] _gates = Enumerable.Range(0, StripeCount)
        .Select(_ => new SemaphoreSlim(1, 1)).ToArray();

    public LoginTokenRepository(IVolatileKVStore qrTokens, IVolatileKVStore webTokens)
    {
        _qrTokens = qrTokens;
        qrTokens.SetSchema(new TableDefinition("ferrite", "qr_login_tokens",
            new KeyDefinition("pk",
                new DataColumn { Name = "token", Type = DataType.Bytes })));
        _webTokens = webTokens;
        webTokens.SetSchema(new TableDefinition("ferrite", "web_authorization_tokens",
            new KeyDefinition("pk",
                new DataColumn { Name = "token_digest", Type = DataType.Bytes })));
    }

    private SemaphoreSlim Gate(ReadOnlySpan<byte> key)
    {
        uint hash = 2166136261;
        foreach (byte value in key)
        {
            hash = (hash ^ value) * 16777619;
        }
        return _gates[(int)(hash % StripeCount)];
    }

    public async ValueTask PutQrTokenAsync(TLDto.TLQrLoginToken token, TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        var view = token.AsQrLoginToken();
        byte[] key = view.Token.ToArray();
        byte[] bytes = token.AsSpan().ToArray();
        SemaphoreSlim gate = Gate(key);
        await gate.WaitAsync(cancellationToken);
        try
        {
            _qrTokens.Delete(key);
            _qrTokens.Put(bytes, ttl, key);
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<TLDto.TLQrLoginToken?> GetQrTokenAsync(
        ReadOnlyMemory<byte> token, CancellationToken cancellationToken = default)
    {
        byte[] key = token.ToArray();
        SemaphoreSlim gate = Gate(key);
        await gate.WaitAsync(cancellationToken);
        try
        {
            byte[]? bytes = _qrTokens.Get(key);
            return bytes == null ? null : new TLDto.TLQrLoginToken(bytes, 0, bytes.Length);
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<bool> TryReplaceQrTokenAsync(ReadOnlyMemory<byte> token,
        int expectedState, TLDto.TLQrLoginToken replacement, TimeSpan ttl,
        CancellationToken cancellationToken = default)
    {
        byte[] key = token.ToArray();
        var replacementView = replacement.AsQrLoginToken();
        byte[] replacementKey = replacementView.Token.ToArray();
        byte[] replacementBytes = replacement.AsSpan().ToArray();
        if (!key.AsSpan().SequenceEqual(replacementKey))
        {
            throw new ArgumentException("The replacement must retain the same QR token.",
                nameof(replacement));
        }

        SemaphoreSlim gate = Gate(key);
        await gate.WaitAsync(cancellationToken);
        try
        {
            byte[]? current = _qrTokens.Get(key);
            if (current == null ||
                new TLDto.TLQrLoginToken(current, 0, current.Length)
                    .AsQrLoginToken().State != expectedState)
            {
                return false;
            }
            _qrTokens.Delete(key);
            _qrTokens.Put(replacementBytes, ttl, key);
            return true;
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<TLDto.TLQrLoginToken?> ConsumeQrTokenAsync(
        ReadOnlyMemory<byte> token, int expectedState,
        CancellationToken cancellationToken = default)
    {
        byte[] key = token.ToArray();
        SemaphoreSlim gate = Gate(key);
        await gate.WaitAsync(cancellationToken);
        try
        {
            byte[]? bytes = _qrTokens.Get(key);
            if (bytes == null ||
                new TLDto.TLQrLoginToken(bytes, 0, bytes.Length)
                    .AsQrLoginToken().State != expectedState)
            {
                return null;
            }
            _qrTokens.Delete(key);
            return new TLDto.TLQrLoginToken(bytes, 0, bytes.Length);
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask PutWebTokenAsync(TLDto.TLWebAuthorizationToken token,
        TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var view = token.AsWebAuthorizationToken();
        byte[] key = view.TokenDigest.ToArray();
        byte[] bytes = token.AsSpan().ToArray();
        SemaphoreSlim gate = Gate(key);
        await gate.WaitAsync(cancellationToken);
        try
        {
            _webTokens.Delete(key);
            _webTokens.Put(bytes, ttl, key);
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<TLDto.TLWebAuthorizationToken?> GetWebTokenAsync(
        ReadOnlyMemory<byte> tokenDigest, CancellationToken cancellationToken = default)
    {
        byte[] key = tokenDigest.ToArray();
        SemaphoreSlim gate = Gate(key);
        await gate.WaitAsync(cancellationToken);
        try
        {
            byte[]? bytes = _webTokens.Get(key);
            return bytes == null
                ? null
                : new TLDto.TLWebAuthorizationToken(bytes, 0, bytes.Length);
        }
        finally
        {
            gate.Release();
        }
    }

    public async ValueTask<TLDto.TLWebAuthorizationToken?> ConsumeWebTokenAsync(
        ReadOnlyMemory<byte> tokenDigest, CancellationToken cancellationToken = default)
    {
        byte[] key = tokenDigest.ToArray();
        SemaphoreSlim gate = Gate(key);
        await gate.WaitAsync(cancellationToken);
        try
        {
            byte[]? bytes = _webTokens.Get(key);
            if (bytes == null)
            {
                return null;
            }
            _webTokens.Delete(key);
            return new TLDto.TLWebAuthorizationToken(bytes, 0, bytes.Length);
        }
        finally
        {
            gate.Release();
        }
    }
}
