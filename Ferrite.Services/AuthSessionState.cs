// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;
using TlAuthSessionState = Ferrite.TL.baseLayer.dto.AuthSessionState;

namespace Ferrite.Services;

public class AuthSessionState
{
    public Guid NodeId { get; set; }
    public int Stage { get; set; }
    public byte[] Nonce { get; set; } = [];
    public byte[] ServerNonce { get; set; } = [];
    public int? P { get; set; }
    public int? Q { get; set; }
    public byte[]? NewNonce { get; set; }
    public byte[]? TempAesKey { get; set; }
    public byte[]? TempAesIv { get; set; }
    public bool TempAuthKey { get; set; }
    public int? TempAuthKeyExpiresIn { get; set; }
    public int? ValidUntil { get; set; }
    public int? G { get; set; }
    public byte[]? A { get; set; }
    public byte[]? GA { get; set; }
    public byte[]? AuthKey { get; set; }

    public static AuthSessionState FromSessionData(Guid nodeId,
        IReadOnlyDictionary<string, object> values)
    {
        var state = new AuthSessionState
        {
            NodeId = nodeId,
            Nonce = Bytes(values, "nonce") ?? [],
            ServerNonce = Bytes(values, "server_nonce") ?? [],
            P = Integer(values, "p"),
            Q = Integer(values, "q"),
            NewNonce = Bytes(values, "new_nonce"),
            TempAesKey = Bytes(values, "temp_aes_key"),
            TempAesIv = Bytes(values, "temp_aes_iv"),
            TempAuthKey = Boolean(values, "temp_auth_key"),
            TempAuthKeyExpiresIn = Integer(values, "temp_auth_key_expires_in"),
            ValidUntil = UnixSeconds(values, "valid_until"),
            G = Integer(values, "g"),
            A = Bytes(values, "a"),
            GA = Bytes(values, "g_a"),
            AuthKey = Bytes(values, "auth_key"),
        };
        state.Stage = state.AuthKey != null ? 3
            : state.TempAesKey != null ? 2
            : 1;
        return state;
    }

    public void RestoreInto(IDictionary<string, object> values)
    {
        Add(values, "nonce", Nonce);
        Add(values, "server_nonce", ServerNonce);
        Add(values, "p", P);
        Add(values, "q", Q);
        Add(values, "new_nonce", NewNonce);
        Add(values, "temp_aes_key", TempAesKey);
        Add(values, "temp_aes_iv", TempAesIv);
        if (TempAuthKey) values.TryAdd("temp_auth_key", true);
        Add(values, "temp_auth_key_expires_in", TempAuthKeyExpiresIn);
        if (ValidUntil is { } validUntil)
        {
            values.TryAdd("valid_until",
                DateTimeOffset.FromUnixTimeSeconds(validUntil).LocalDateTime);
        }
        Add(values, "g", G);
        Add(values, "a", A);
        Add(values, "g_a", GA);
        Add(values, "auth_key", AuthKey);
    }

    public TLAuthSessionState ToTl()
    {
        var builder = TlAuthSessionState.Builder()
            .NodeId(NodeId.ToByteArray())
            .Stage(Stage)
            .Nonce(Nonce)
            .ServerNonce(ServerNonce)
            .TempAuthKey(TempAuthKey);
        if (P is { } p) builder.P(p);
        if (Q is { } q) builder.Q(q);
        if (NewNonce != null) builder.NewNonce(NewNonce);
        if (TempAesKey != null) builder.TempAesKey(TempAesKey);
        if (TempAesIv != null) builder.TempAesIv(TempAesIv);
        if (TempAuthKeyExpiresIn is { } expiresIn)
            builder.TempAuthKeyExpiresIn(expiresIn);
        if (ValidUntil is { } validUntil) builder.ValidUntil(validUntil);
        if (G is { } g) builder.G(g);
        if (A != null) builder.A(A);
        if (GA != null) builder.GA(GA);
        if (AuthKey != null) builder.AuthKey(AuthKey);
        TlAuthSessionState row = builder.Build();
        return row;
    }

    public static AuthSessionState FromTl(TLAuthSessionState row)
    {
        TlAuthSessionState view = row.AsAuthSessionState();
        if (view.NodeId.Length != 16)
        {
            throw new InvalidDataException("Auth session node id must be 16 bytes.");
        }
        return new AuthSessionState
        {
            NodeId = new Guid(view.NodeId),
            Stage = view.Stage,
            Nonce = view.Nonce.ToArray(),
            ServerNonce = view.ServerNonce.ToArray(),
            P = view.Flags[0] ? view.P : null,
            Q = view.Flags[1] ? view.Q : null,
            NewNonce = view.Flags[2] ? view.NewNonce.ToArray() : null,
            TempAesKey = view.Flags[3] ? view.TempAesKey.ToArray() : null,
            TempAesIv = view.Flags[4] ? view.TempAesIv.ToArray() : null,
            TempAuthKey = view.TempAuthKey,
            TempAuthKeyExpiresIn = view.Flags[6] ? view.TempAuthKeyExpiresIn : null,
            ValidUntil = view.Flags[7] ? view.ValidUntil : null,
            G = view.Flags[8] ? view.G : null,
            A = view.Flags[9] ? view.A.ToArray() : null,
            GA = view.Flags[10] ? view.GA.ToArray() : null,
            AuthKey = view.Flags[11] ? view.AuthKey.ToArray() : null,
        };
    }

    private static byte[]? Bytes(IReadOnlyDictionary<string, object> values,
        string key) => values.TryGetValue(key, out object? value)
        ? value as byte[]
        : null;

    private static int? Integer(IReadOnlyDictionary<string, object> values,
        string key) => values.TryGetValue(key, out object? value) && value is int number
        ? number
        : null;

    private static bool Boolean(IReadOnlyDictionary<string, object> values,
        string key) => values.TryGetValue(key, out object? value) && value is true;

    private static int? UnixSeconds(IReadOnlyDictionary<string, object> values,
        string key) => values.TryGetValue(key, out object? value) && value is DateTime date
        ? checked((int)new DateTimeOffset(date).ToUnixTimeSeconds())
        : null;

    private static void Add(IDictionary<string, object> values, string key,
        object? value)
    {
        if (value != null) values.TryAdd(key, value);
    }
}
