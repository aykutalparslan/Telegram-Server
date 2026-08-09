// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL;
using Ferrite.TL.baseLayer.account;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class UpdateThemeHandler : ThemeHandlerBase
{
    public UpdateThemeHandler(ThemeStore store, ProfileStore profiles)
        : base(store, profiles) { }

    [TLFunction(Constructors.baseLayer_AccountUpdateTheme)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await GetUserIdAsync(authKeyId);
        if (!userId.HasValue) return AuthError();
        var request = new AccountUpdateTheme(q.AsSpan());
        if (!TryReadTheme(request.Get_ThemeView(), out ThemeInput input))
            return Invalid();
        ThemeDocumentInput? document = null;
        if (request.Flags[2])
        {
            if (!TryReadDocument(request.Get_DocumentView(),
                    out var parsedDocument)) return Invalid();
            document = parsedDocument;
        }
        List<ThemeSettingsInput>? settings = null;
        if (request.Flags[3] && !TryReadSettings(request.Settings,
                out settings)) return Invalid();
        string format = Encoding.UTF8.GetString(request.Format).Trim();
        string? slug = request.Flags[0]
            ? Encoding.UTF8.GetString(request.Slug) : null;
        string? title = request.Flags[1]
            ? Encoding.UTF8.GetString(request.Title).Trim() : null;
        try
        {
            return await Store.UpdateAsync(userId.Value, format, input, slug,
                title, document, settings);
        }
        finally
        {
            if (settings is not null) Dispose(settings);
        }
    }
}
