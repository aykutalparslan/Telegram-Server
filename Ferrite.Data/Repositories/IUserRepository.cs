// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;

namespace Ferrite.Data.Repositories;

public interface IUserRepository
{
    public bool PutUser(TLUser user);
    public bool UpdateUsername(long userId, string username);
    public bool UpdateUserPhone(long userId, string phone);
    public TLUser? GetUser(long userId);
    public TLUser? GetUser(string phone);
    public long? GetUserId(string phone);
    public TLUser? GetUserByUsername(string username);
    public bool DeleteUser(long userId);
    public bool UpdateAccountTtl(long userId, int accountDaysTTL);
    public int GetAccountTtl(long userId);
    public bool PutAbout(long userId, string about);
    public string? GetAbout(long userId);
}