// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using TLUserStatus = Ferrite.TL.baseLayer.TLUserStatus;

namespace Ferrite.Data.Repositories;

public class UserStatusRepository : IUserStatusRepository
{
    private const int OnlineStatusExpiresInSeconds = 60;
    private const int OneDayInSeconds = 24 * 60 * 60;
    private const int OneWeekInSeconds = 7 * OneDayInSeconds;
    private const int OneMonthInSeconds = 30 * OneDayInSeconds;
    private readonly IKVStore _store;
    public UserStatusRepository(IKVStore store)
    {
        _store = store;
        _store.SetSchema(new TableDefinition("ferrite", "user_statuses",
            new KeyDefinition("pk",
                new DataColumn { Name = "user_id", Type = DataType.Long })));
    }
    public bool PutUserStatus(long userId, bool status)
    {
        var now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var statusBytes = UserStatusFull.Builder()
                .Status(status)
                .WasOnline(now)
                .Expires(status ? now + OnlineStatusExpiresInSeconds : now)
                .Build();
        return _store.Put(statusBytes.ToReadOnlySpan().ToArray(), userId);
    }

    public async ValueTask<TLUserStatus> GetUserStatusAsync(long userId)
    {
        return Interpret(await _store.GetAsync(userId));
    }

    public TLUserStatus GetUserStatus(long userId)
    {
        return Interpret(_store.Get(userId));
    }

    private static TLUserStatus Interpret(byte[]? serialized)
    {
        if (serialized == null)
        {
            return new UserStatusEmpty();
        }

        var userStatus = new TLUserStatusFull(serialized, 0, serialized.Length);
        var full = userStatus.AsUserStatusFull();
        int wasOnline = full.WasOnline;
        int now = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (full.Status && full.Expires > now)
        {
            return new UserStatusOnline(full.Expires);
        }

        if (wasOnline >= now - OneDayInSeconds)
        {
            return new UserStatusOffline(wasOnline);
        }
        if (wasOnline >= now - OneWeekInSeconds)
        {
            return new UserStatusRecently();
        }
        if (wasOnline >= now - OneMonthInSeconds)
        {
            return new UserStatusLastWeek();
        }
        return new UserStatusLastMonth();
    }
}
