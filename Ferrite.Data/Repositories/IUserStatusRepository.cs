// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;

namespace Ferrite.Data.Repositories;

public interface IUserStatusRepository
{
    public bool PutUserStatus(long userId, bool status);
    public ValueTask<TLUserStatus> GetUserStatusAsync(long userId);
}