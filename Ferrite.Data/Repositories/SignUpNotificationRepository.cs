// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Data.Repositories;

using Ferrite.TL;
using Ferrite.TL.baseLayer.dto;

public class SignUpNotificationRepository : ISignUpNotificationRepository
{
    private readonly IKVStore _store;
    public SignUpNotificationRepository(IKVStore store)
    {
        _store = store;
        _store.SetSchema(new TableDefinition("ferrite", "signup_notifications_tl1",
            new KeyDefinition("pk",
                new DataColumn { Name = "user_id", Type = DataType.Long })));
    }
    public bool PutSignUpNotification(long userId, bool silent)
    {
        using var row = SignUpNotificationState.Builder().Silent(silent).Build();
        return _store.Put(row.ToReadOnlySpan().ToArray(), userId);
    }

    public bool GetSignUpNotification(long userId)
    {
        var val = _store.Get(userId);
        if (val == null) return false;
        var value = new TLBytes(val, 0, val.Length);
        if (value.Constructor != Constructors.baseLayer_SignUpNotificationState)
            throw new InvalidDataException("Sign-up notification codec/version mismatch.");
        return ((TLSignUpNotificationState)value).AsSignUpNotificationState().Silent;
    }
}
