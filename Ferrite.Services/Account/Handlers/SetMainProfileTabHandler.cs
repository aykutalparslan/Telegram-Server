// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.account;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class SetMainProfileTabHandler : ProfileSettingsHandlerBase
{
    public SetMainProfileTabHandler(ProfileStore store) : base(store) { }

    [TLFunction(Constructors.baseLayer_AccountSetMainProfileTab)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await GetUserIdAsync(authKeyId);
        if (!userId.HasValue) return AuthError();
        ProfileTabView view = new AccountSetMainProfileTab(q.AsSpan())
            .Get_TabView();
        using TLProfileTab? tab = Clone(view);
        if (tab is null) return Invalid("PROFILE_TAB_INVALID"u8);
        return await Store.SetMainProfileTabAsync(userId.Value, tab.Value);
    }

    private static TLProfileTab? Clone(ProfileTabView view)
    {
        if (view.Is(out ProfileTabPosts value0)) return value0.Clone().Build();
        if (view.Is(out ProfileTabGifts value1)) return value1.Clone().Build();
        if (view.Is(out ProfileTabMedia value2)) return value2.Clone().Build();
        if (view.Is(out ProfileTabFiles value3)) return value3.Clone().Build();
        if (view.Is(out ProfileTabMusic value4)) return value4.Clone().Build();
        if (view.Is(out ProfileTabVoice value5)) return value5.Clone().Build();
        if (view.Is(out ProfileTabLinks value6)) return value6.Clone().Build();
        if (view.Is(out ProfileTabGifs value7)) return value7.Clone().Build();
        return null;
    }
}
