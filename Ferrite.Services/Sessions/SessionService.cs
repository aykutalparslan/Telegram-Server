// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System;
using System.Collections.Concurrent;
using Ferrite.Data.Repositories;
using Ferrite.Utils;
using TLAuthSessionState = Ferrite.TL.baseLayer.dto.TLAuthSessionState;
using TLRemoteSession = Ferrite.TL.baseLayer.dto.TLRemoteSession;

namespace Ferrite.Services.Sessions;

public class SessionService : ISessionService
{
    private readonly IAuthSessionRepository _authSessionRepository;
    private readonly ISessionRepository _sessionRepository;

    public Guid NodeId { get; private set; }
    private readonly ConcurrentDictionary<long, ActiveSession> _localSessions = new();
    private static readonly long SessionRefreshSeconds = FerriteConfig.SessionTTL / 10;
    private readonly ConcurrentDictionary<long, long> _leaseWrittenAt = new();
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

        _localSessions[sessionId] = session;
        if (!ShouldWriteLease(sessionId))
        {
            return true;
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
        return remoteAdd && authKeyAdd;
    }

    public bool AddSession(long authKeyId, long sessionId, ActiveSession session)
    {
        if (sessionId == 0)
        {
            return false;
        }

        _localSessions[sessionId] = session;
        if (!ShouldWriteLease(sessionId))
        {
            return true;
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
        return remoteAdd && authKeyAdd;
    }

    private bool ShouldWriteLease(long sessionId)
    {
        long now = Environment.TickCount64 / 1000;
        if (_leaseWrittenAt.TryGetValue(sessionId, out long written) &&
            now - written < SessionRefreshSeconds)
        {
            return false;
        }
        _leaseWrittenAt[sessionId] = now;
        return true;
    }

    private void ForgetLease(long sessionId) =>
        _leaseWrittenAt.TryRemove(sessionId, out _);

    public async Task<RemoteSession?> GetSessionStateAsync(long sessionId)
    {
        return await GetSessionState(sessionId);
    }

    public async Task<bool> DeleteSessionAsync(long sessionId)
    {
        _localSessions.TryRemove(sessionId, out var removed);
        ForgetLease(sessionId);
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
        ForgetLease(sessionId);
        _sessionRepository.DeleteSession(sessionId);
        _sessionRepository.DeleteSessionForAuthKey(authKeyId, sessionId);
        await _unitOfWork.SaveAsync();
        return _localSessions.TryRemove(sessionId, out var session);
    }

    public async Task<bool> RemoveSession(long authKeyId, long permAuthKeyId,
        long sessionId, IMTProtoConnection owner)
    {
        if (!_localSessions.TryGetValue(sessionId, out var active) ||
            !active.TryGetConnection(out var currentOwner) ||
            !ReferenceEquals(currentOwner, owner) ||
            !((ICollection<KeyValuePair<long, ActiveSession>>)_localSessions)
                .Remove(new KeyValuePair<long, ActiveSession>(sessionId, active)))
        {
            return false;
        }

        ForgetLease(sessionId);
        _sessionRepository.DeleteSession(sessionId);
        _sessionRepository.DeleteSessionForAuthKey(authKeyId, sessionId);
        if (permAuthKeyId != 0 && permAuthKeyId != authKeyId)
        {
            _sessionRepository.DeleteSessionForAuthKey(permAuthKeyId, sessionId);
        }
        await _unitOfWork.SaveAsync();
        return true;
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

        var state = new RemoteSession
        {
            SessionId = sessionId,
            NodeId = NodeId,
        };
        using TLRemoteSession remoteState = state.ToTl();
        var sessionSaved = _sessionRepository.PutSession(remoteState,
            TimeSpan.FromSeconds(FerriteConfig.SessionTTL));
        var authKeySaved = _sessionRepository.PutSessionForAuthKey(authKeyId, sessionId);
        _log.Debug($"=== Put Session for Auth Key: {authKeyId} ===");
        await _unitOfWork.SaveAsync();
        return sessionSaved && authKeySaved;
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
