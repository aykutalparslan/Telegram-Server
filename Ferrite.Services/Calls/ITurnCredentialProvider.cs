// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Security.Cryptography;
using System.Text;

namespace Ferrite.Services.Calls;

public sealed record TurnCredentials(string Username, string Password,
    long ExpiresAtUnix);

public interface ITurnCredentialProvider
{
    bool IsEnabled { get; }

    TurnCredentials? Create(long callId);
}

public sealed class CoturnRestCredentialProvider : ITurnCredentialProvider
{
    private readonly CallTurnOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly bool _valid;

    public CoturnRestCredentialProvider(CallTurnOptions options,
        TimeProvider timeProvider)
    {
        _options = options;
        _timeProvider = timeProvider;
        _valid = options.TryValidate(out _);
    }

    public bool IsEnabled => _options.Enabled && _valid;

    public TurnCredentials? Create(long callId)
    {
        if (!IsEnabled)
        {
            return null;
        }

        long expiresAt = _timeProvider.GetUtcNow().ToUnixTimeSeconds() +
                         (long)_options.CredentialTtl.TotalSeconds;
        string username = $"{expiresAt}:call{callId}";
        using var hmac = new HMACSHA1(Encoding.UTF8.GetBytes(_options.SharedSecret));
        string password = Convert.ToBase64String(
            hmac.ComputeHash(Encoding.UTF8.GetBytes(username)));
        return new TurnCredentials(username, password, expiresAt);
    }
}
