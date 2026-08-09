// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Data.Repositories;

using Ferrite.TL.baseLayer.dto;

public interface ISessionRepository
{
    public bool PutSession(TLRemoteSession session, TimeSpan expire);
    public TLRemoteSession? GetSession(long sessionId);
    public bool SetSessionTTL(long sessionId, TimeSpan expire);
    public bool DeleteSession(long sessionId);
    public bool PutSessionForAuthKey(long authKeyId, long sessionId);
    public bool DeleteSessionForAuthKey(long authKeyId, long sessionId);
    public ICollection<long> GetSessionsByAuthKey(long authKeyId, TimeSpan expire);
}
