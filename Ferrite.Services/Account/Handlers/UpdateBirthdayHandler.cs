// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.account;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class UpdateBirthdayHandler : ProfileSettingsHandlerBase
{
    public UpdateBirthdayHandler(ProfileStore store) : base(store) { }

    [TLFunction(Constructors.baseLayer_UpdateBirthday)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await GetUserIdAsync(authKeyId);
        if (!userId.HasValue) return AuthError();
        var request = new UpdateBirthday(q.AsSpan());
        if (!request.Flags[0])
            return await Store.UpdateBirthdayAsync(userId.Value, null);
        Birthday value = (Birthday)request.Birthday;
        int year = value.Flags[0] ? value.Year : 2000;
        if (value.Month is < 1 or > 12 || value.Day < 1 ||
            value.Day > DateTime.DaysInMonth(year, value.Month) ||
            value.Flags[0] && value.Year is < 1800 or > 3000)
            return Invalid("BIRTHDAY_INVALID"u8);
        using TLBirthday birthday = value.Clone().Build();
        return await Store.UpdateBirthdayAsync(userId.Value, birthday);
    }
}
