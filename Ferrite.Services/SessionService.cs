// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System;
using System.Collections.Concurrent;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Utils;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services;

public class SessionService : ISessionService
{
    private readonly IAuthSessionRepository _authSessionRepository;
    private readonly ISessionRepository _sessionRepository;

    public Guid NodeId { get; private set; }
    private readonly ConcurrentDictionary<long, ActiveSession> _localSessions = new();
    private readonly ConcurrentDictionary<Nonce, ActiveSession> _localAuthSessions = new();
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger _log;
    private Guid GetNodeId()
    {
        if (File.Exists("node.guid"))
        {
            var bytes = File.ReadAllBytes("node.guid");
            return new Guid(bytes);
        }
        else
        {
            var guid = Guid.NewGuid();
            File.WriteAllBytes("node.guid", guid.ToByteArray());
            return guid;
        }
    }
    public SessionService(IUnitOfWork unitOfWork, IAuthSessionRepository authSessionRepository, ISessionRepository sessionRepository, ILogger log)
        : this(unitOfWork, authSessionRepository, sessionRepository, log, null)
    {
    }

    public SessionService(IUnitOfWork unitOfWork, IAuthSessionRepository authSessionRepository, ISessionRepository sessionRepository, ILogger log, Guid? nodeId)
    {
        _authSessionRepository = authSessionRepository;
        _sessionRepository = sessionRepository;

        NodeId = nodeId ?? GetNodeId();
        _unitOfWork = unitOfWork;
        _log = log;
    }
    public async Task<bool> AddSessionAsync(long authKeyId, long sessionId, ActiveSession session)
    {
        if (sessionId == 0)
        {
            return false;
        }

        var state = new RemoteSession
        {
            SessionId = sessionId,
            NodeId = NodeId,
        };
        using TLRemoteSession remoteState = state.ToTl();
        var remoteAdd = _sessionRepository.PutSession(remoteState,
            new TimeSpan(0,0, FerriteConfig.SessionTTL));
        var authKeyAdd = _sessionRepository.PutSessionForAuthKey(authKeyId, state.SessionId);
        _log.Debug($"=== 1 = Put Session for Auth Key: {authKeyId} ===");
        await _unitOfWork.SaveAsync();
        if (_localSessions.ContainsKey(state.SessionId))
        {
            _localSessions.Remove(state.SessionId, out var value);
        }
        return remoteAdd && authKeyAdd && _localSessions.TryAdd(state.SessionId, session);
    }

    public bool AddSession(long authKeyId, long sessionId, ActiveSession session)
    {
        if (sessionId == 0)
        {
            return false;
        }

        var state = new RemoteSession
        {
            SessionId = sessionId,
            NodeId = NodeId,
        };
        using TLRemoteSession remoteState = state.ToTl();
        var remoteAdd = _sessionRepository.PutSession(remoteState,
            new TimeSpan(0,0, FerriteConfig.SessionTTL));
        var authKeyAdd = _sessionRepository.PutSessionForAuthKey(authKeyId, state.SessionId);
        _log.Debug($"=== 2 = Put Session for Auth Key: {authKeyId} ===");
        _unitOfWork.Save();
        if (_localSessions.ContainsKey(state.SessionId))
        {
            _localSessions.Remove(state.SessionId, out var value);
        }

        var localAdd = _localSessions.TryAdd(state.SessionId, session);
        return remoteAdd && authKeyAdd && localAdd;
    }

    public async Task<RemoteSession?> GetSessionStateAsync(long sessionId)
    {
        return await GetSessionState(sessionId);
    }

    public async Task<bool> DeleteSessionAsync(long sessionId)
    {
        _localSessions.TryRemove(sessionId, out var removed);
        _sessionRepository.DeleteSession(sessionId);
        await _unitOfWork.SaveAsync();
        return true;
    }

    private async Task<RemoteSession> GetSessionState(long sessionId)
    {
        using TLRemoteSession? row = _sessionRepository.GetSession(sessionId);
        if (row != null)
        {
            return RemoteSession.FromTl(row.Value);
        }
        return null;
    }

    public async Task<bool> RemoveSession(long authKeyId, long sessionId)
    {
        _sessionRepository.DeleteSession(sessionId);
        _sessionRepository.DeleteSessionForAuthKey(authKeyId, sessionId);
        await _unitOfWork.SaveAsync();
        return _localSessions.TryRemove(sessionId, out var session);
    }
    public bool LocalSessionExists(long sessionId)
    {
        return _localSessions.ContainsKey(sessionId);
    }
    public bool TryGetLocalSession(long sessionId, out ActiveSession session)
    {
        return _localSessions.TryGetValue(sessionId, out session);
    }

    public async Task<bool> AddAuthSessionAsync(byte[] nonce, AuthSessionState state, ActiveSession session)
    {
        state.NodeId = NodeId;
        using TLAuthSessionState row = state.ToTl();
        var remoteAdd = _authSessionRepository.PutAuthKeySession(nonce, row);
        await _unitOfWork.SaveAsync();
        var key = (Nonce)nonce;
        if (_localAuthSessions.ContainsKey(key))
        {
            _localAuthSessions.Remove(key, out var value);
        }
        return remoteAdd && _localAuthSessions.TryAdd((Nonce)nonce, session);
    }

    public async Task<bool> UpdateAuthSessionAsync(byte[] nonce, AuthSessionState state)
    {
        using TLAuthSessionState row = state.ToTl();
        bool result = _authSessionRepository.PutAuthKeySession(nonce, row);
        await _unitOfWork.SaveAsync();
        return result;
    }

    public async Task<AuthSessionState?> GetAuthSessionStateAsync(byte[] nonce)
    {
        using TLAuthSessionState? row = _authSessionRepository.GetAuthKeySession(nonce);
        if (row != null)
        {
            return AuthSessionState.FromTl(row.Value);
        }
        return null;
    }

    public bool LocalAuthSessionExists(byte[] nonce)
    {
        return _localAuthSessions.ContainsKey((Nonce)nonce);
    }

    public bool TryGetLocalAuthSession(byte[] nonce, out ActiveSession session)
    {
        return _localAuthSessions.TryGetValue((Nonce)nonce, out session);
    }

    public bool RemoveAuthSession(byte[] nonce)
    {
        _authSessionRepository.RemoveAuthKeySession(nonce);
        _unitOfWork.Save();
        return _localAuthSessions.TryRemove((Nonce)nonce, out var a);
    }

    public async Task<bool> OnPing(long authKeyId, long sessionId)
    {
        if (sessionId == 0)
        {
            return false;
        }

        var ttlSet = _sessionRepository.SetSessionTTL(sessionId, new TimeSpan(0, 0, FerriteConfig.SessionTTL));
        bool sessionSaved = _sessionRepository.PutSessionForAuthKey(authKeyId, sessionId);
        _log.Debug($"=== Put Session for Auth Key: {authKeyId} ===");
        await _unitOfWork.SaveAsync();
        return ttlSet && sessionSaved;
    }

    public async Task<ICollection<RemoteSession>> GetSessionsAsync(long authKeyId)
    {
        var sessionIds = _sessionRepository.GetSessionsByAuthKey(authKeyId,
            TimeSpan.FromSeconds(FerriteConfig.SessionTTL));
        _log.Debug($"=== Got {sessionIds.Count} sessions for Auth Key: {authKeyId} ===");
        List<RemoteSession> result = new();
        foreach (var sessionId in sessionIds)
        {
            if (sessionId == 0)
            {
                continue;
            }

            var state = await GetSessionState(sessionId);
            if (state is { SessionId: not 0 })
            {
                result.Add(state);
            }
        }
        return result;
    }
}
