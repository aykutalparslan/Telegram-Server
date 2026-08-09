// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services;

public interface ISessionService
{
    Guid NodeId { get; }

    Task<bool> AddSessionAsync(long authKeyId, long sessionId, ActiveSession session);
    bool AddSession(long authKeyId, long sessionId, ActiveSession session);
    Task<RemoteSession?> GetSessionStateAsync(long sessionId);
    Task<bool> DeleteSessionAsync(long sessionId);
    Task<ICollection<RemoteSession>> GetSessionsAsync(long authKeyId);
    Task<bool> AddAuthSessionAsync(byte[] nonce, AuthSessionState state, ActiveSession session);
    public Task<bool> UpdateAuthSessionAsync(byte[] nonce, AuthSessionState state);
    Task<AuthSessionState?> GetAuthSessionStateAsync(byte[] nonce);
    bool LocalSessionExists(long sessionId);
    bool LocalAuthSessionExists(byte[] nonce);
    Task<bool> RemoveSession(long authKeyId, long sessionId);
    Task<bool> OnPing(long authKeyId, long sessionId);
    bool RemoveAuthSession(byte[] nonce);
    bool TryGetLocalSession(long sessionId, out ActiveSession session);
    bool TryGetLocalAuthSession(byte[] nonce, out ActiveSession session);
}
