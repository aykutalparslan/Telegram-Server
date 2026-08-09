// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL.baseLayer;

namespace Ferrite.Services.Calls;

/// <summary>
/// Reports whether the external coturn endpoint should currently be
/// advertised. A health failure only omits WebRTC rows from new confirms; it
/// never aborts a confirmed call that still has a healthy reflector.
/// </summary>
public interface ITurnEndpointHealth
{
    bool IsHealthy { get; }
}

public sealed class StaticTurnEndpointHealth : ITurnEndpointHealth
{
    public StaticTurnEndpointHealth(bool healthy)
    {
        IsHealthy = healthy;
    }

    public bool IsHealthy { get; }
}

/// <summary>
/// Builds the serialized phoneConnectionWebrtc rows for one call. The pinned
/// Android client classifies a row solely by its turn flag and ignores a
/// simultaneous stun flag, so the TURN row (with credentials) and the STUN
/// row (without) are always two distinct rows with distinct stable nonzero
/// ids, never one combined row.
/// </summary>
public sealed class CallTurnConnectionBuilder
{
    private readonly CallTurnOptions _options;
    private readonly ITurnCredentialProvider _credentials;
    private readonly ITurnEndpointHealth _health;
    private readonly bool _valid;

    public CallTurnConnectionBuilder(CallTurnOptions options,
        ITurnCredentialProvider credentials, ITurnEndpointHealth health)
    {
        _options = options;
        _credentials = credentials;
        _health = health;
        _valid = options.TryValidate(out _);
    }

    public List<byte[]> BuildConnections(long callId)
    {
        var rows = new List<byte[]>();
        if (!_options.Enabled || !_valid || !_health.IsHealthy)
        {
            return rows;
        }

        TurnCredentials? credentials = _credentials.Create(callId);
        if (credentials == null)
        {
            return rows;
        }

        byte[] ipv4 = Encoding.UTF8.GetBytes(_options.AdvertisedIPv4);
        byte[] ipv6 = Encoding.UTF8.GetBytes(_options.AdvertisedIPv6);
        using (var turnRow = PhoneConnectionWebrtc.Builder()
                   .Turn(true)
                   .Stun(false)
                   .Id(_options.ConnectionIdSeed)
                   .Ip(ipv4)
                   .Ipv6(ipv6)
                   .Port(_options.Port)
                   .Username(Encoding.UTF8.GetBytes(credentials.Username))
                   .Password(Encoding.UTF8.GetBytes(credentials.Password))
                   .Build())
        {
            rows.Add(turnRow.ToReadOnlySpan().ToArray());
        }

        using (var stunRow = PhoneConnectionWebrtc.Builder()
                   .Turn(false)
                   .Stun(true)
                   .Id(_options.ConnectionIdSeed + 1)
                   .Ip(ipv4)
                   .Ipv6(ipv6)
                   .Port(_options.Port)
                   .Username(""u8)
                   .Password(""u8)
                   .Build())
        {
            rows.Add(stunRow.ToReadOnlySpan().ToArray());
        }

        return rows;
    }
}
