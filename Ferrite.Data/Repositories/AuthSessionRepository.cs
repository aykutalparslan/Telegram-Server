// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Data.Repositories;

using Ferrite.TL;
using Ferrite.TL.baseLayer.dto;

public class AuthSessionRepository : IAuthSessionRepository
{
    private readonly IVolatileKVStore _store;
    public AuthSessionRepository(IVolatileKVStore store)
    {
        _store = store;
        store.SetSchema(new TableDefinition("ferrite", "auth_sessions_tl1",
            new KeyDefinition("pk",
                new DataColumn { Name = "nonce", Type = DataType.Bytes })));
    }
    public bool PutAuthKeySession(byte[] nonce, TLAuthSessionState session)
    {
        _store.Put(session.AsSpan().ToArray(), null, nonce);
        return true;
    }

    public TLAuthSessionState? GetAuthKeySession(byte[] nonce)
    {
        byte[]? bytes = _store.Get(nonce);
        if (bytes is not { Length: > 0 }) return null;
        var value = new TLBytes(bytes, 0, bytes.Length);
        return value.Constructor == Constructors.baseLayer_AuthSessionState
            ? (TLAuthSessionState)value
            : throw new InvalidDataException("Auth session codec/version mismatch.");
    }

    public bool RemoveAuthKeySession(byte[] nonce)
    {
        _store.Delete(nonce);
        return true;
    }
}
