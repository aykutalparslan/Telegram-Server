// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Data.Repositories;

using Ferrite.TL;
using Ferrite.TL.baseLayer.dto;

public class SessionRepository : ISessionRepository
{
    private readonly IVolatileKVStore _store;
    private readonly IVolatileKVStore _storeByAuthKey;
    public SessionRepository(IVolatileKVStore store, IVolatileKVStore storeByAuthKey)
    {
        _store = store;
        _store.SetSchema(new TableDefinition("ferrite", "sessions_tl1",
            new KeyDefinition("pk",
                new DataColumn { Name = "session_id", Type = DataType.Long })));
        _storeByAuthKey = storeByAuthKey;
        _storeByAuthKey.SetSchema(new TableDefinition("ferrite", "sessions_by_auth_key_tl1",
            new KeyDefinition("pk",
                new DataColumn { Name = "auth_key_id", Type = DataType.Long })));
    }
    public bool PutSession(TLRemoteSession session, TimeSpan expire)
    {
        var row = session.AsRemoteSession();
        _store.Put(session.AsSpan().ToArray(), expire, row.SessionId);
        return true;
    }

    public TLRemoteSession? GetSession(long sessionId)
    {
        byte[]? bytes = _store.Get(sessionId);
        if (bytes is not { Length: > 0 }) return null;
        var value = new TLBytes(bytes, 0, bytes.Length);
        return value.Constructor == Constructors.baseLayer_RemoteSession
            ? (TLRemoteSession)value
            : throw new InvalidDataException("Remote session codec/version mismatch.");
    }

    public bool SetSessionTTL(long sessionId, TimeSpan expire)
    {
        _store.UpdateTtl(expire, sessionId);
        return true;
    }

    public bool DeleteSession(long sessionId)
    {
        _store.Delete(sessionId);
        return true;
    }

    public bool PutSessionForAuthKey(long authKeyId, long sessionId)
    {
        using var reference = SessionReference.Builder().SessionId(sessionId).Build();
        return _storeByAuthKey.ListAdd(DateTimeOffset.Now.ToUnixTimeMilliseconds(),
            reference.ToReadOnlySpan().ToArray(), null, authKeyId);
    }

    public bool DeleteSessionForAuthKey(long authKeyId, long sessionId)
    {
        using var reference = SessionReference.Builder().SessionId(sessionId).Build();
        return _storeByAuthKey.ListDelete(reference.ToReadOnlySpan().ToArray(), authKeyId);
    }

    public ICollection<long> GetSessionsByAuthKey(long authKeyId, TimeSpan expire)
    {
        var time = DateTimeOffset.Now - expire;
        _storeByAuthKey.ListDeleteByScore(time.ToUnixTimeMilliseconds());
        var result = new List<long>();
        foreach (byte[] bytes in _storeByAuthKey.ListGet(authKeyId))
        {
            var value = new TLBytes(bytes, 0, bytes.Length);
            if (value.Constructor != Constructors.baseLayer_SessionReference)
            {
                throw new InvalidDataException("Session reference codec/version mismatch.");
            }
            result.Add(((TLSessionReference)value).AsSessionReference().SessionId);
        }
        return result;
    }
}
