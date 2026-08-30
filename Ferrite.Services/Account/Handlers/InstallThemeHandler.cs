// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL;
using Ferrite.TL.baseLayer.account;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class InstallThemeHandler : ThemeHandlerBase
{
    public InstallThemeHandler(ThemeStore store, ProfileStore profiles)
        : base(store, profiles) { }

    [TLFunction(Constructors.layer105_AccountInstallTheme)]
    public async Task<TLBytes> HandleLayer105(long authKeyId, TLBytes q)
    {
        using var current = ToCurrentInstallThemeRequest(q);
        return await Handle(authKeyId, current);
    }

    private static TLBytes ToCurrentInstallThemeRequest(TLBytes q)
    {
        var sent = new TL.layer105.account.AccountInstallTheme(q.AsSpan());
        var builder = InstallTheme.Builder().Dark(sent.Dark);
        if (sent.Flags[1])
        {
            builder = builder.Format(sent.Format).Theme(sent.Theme);
        }
        using var current = builder.Build();
        return current.TLBytes!.Value;
    }

    [TLFunction(Constructors.baseLayer_InstallTheme)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await GetUserIdAsync(authKeyId);
        if (!userId.HasValue) return AuthError();
        var request = new InstallTheme(q.AsSpan());
        ThemeInput? input = null;
        if (request.Flags[1])
        {
            if (!TryReadTheme(request.Get_ThemeView(), out var parsed))
                return Invalid();
            input = parsed;
        }
        if (request.Flags[3] && !ValidBaseTheme(request.Get_BaseThemeView()))
            return Invalid();
        string? format = request.Flags[2]
            ? Encoding.UTF8.GetString(request.Format).Trim() : null;
        return await Store.InstallAsync(userId.Value, request.Dark, input,
            format);
    }
}
