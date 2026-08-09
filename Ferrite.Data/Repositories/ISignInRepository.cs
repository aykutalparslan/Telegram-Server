// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Data.Repositories;

public interface ISignInRepository
{
    public bool PutSignIn(long authKeyId, string phoneNumber, string phoneCodeHash);
    public long GetSignIn(string phoneNumber, string phoneCodeHash);
}