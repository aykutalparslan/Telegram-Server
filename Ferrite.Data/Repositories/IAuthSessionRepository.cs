// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Data.Repositories;

using Ferrite.TL.baseLayer.dto;

public interface IAuthSessionRepository
{
    public bool PutAuthKeySession(byte[] nonce, TLAuthSessionState session);
    public TLAuthSessionState? GetAuthKeySession(byte[] nonce);
    public bool RemoveAuthKeySession(byte[] nonce);
}
