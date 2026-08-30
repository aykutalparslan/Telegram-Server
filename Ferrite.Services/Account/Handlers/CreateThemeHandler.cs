// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL;
using Ferrite.TL.baseLayer.account;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class CreateThemeHandler : ThemeHandlerBase
{
    public CreateThemeHandler(ThemeStore store, ProfileStore profiles)
        : base(store, profiles) { }

    [TLFunction(Constructors.layer107_AccountCreateTheme)]
    public async Task<TLBytes> HandleLayer107(long authKeyId, TLBytes q)
    {
        using var current = ToCurrentCreateThemeRequest(q);
        return await Handle(authKeyId, current);
    }

    private static TLBytes ToCurrentCreateThemeRequest(TLBytes q)
    {
        var sent = new TL.layer107.account.AccountCreateTheme(q.AsSpan());
        var builder = CreateTheme.Builder()
            .Slug(sent.Slug)
            .Title(sent.Title);
        if (sent.Flags[2]) builder = builder.Document(sent.Document);
        if (sent.Flags[3])
        {
            var settings = new Vector();
            settings.AppendTLObject(sent.Settings);
            builder = builder.Settings(settings);
        }
        using var current = builder.Build();
        return current.TLBytes!.Value;
    }

    [TLFunction(Constructors.baseLayer_CreateTheme)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await GetUserIdAsync(authKeyId);
        if (!userId.HasValue) return AuthError();
        var request = new CreateTheme(q.AsSpan());
        ThemeDocumentInput? document = null;
        if (request.Flags[2])
        {
            if (!TryReadDocument(request.Get_DocumentView(),
                    out var parsedDocument)) return Invalid();
            document = parsedDocument;
        }
        var settings = new List<ThemeSettingsInput>();
        if (request.Flags[3] && !TryReadSettings(request.Settings, out settings))
            return Invalid();
        string slug = Encoding.UTF8.GetString(request.Slug);
        string title = Encoding.UTF8.GetString(request.Title).Trim();
        try
        {
            return await Store.CreateAsync(userId.Value, slug, title, document,
                settings);
        }
        finally
        {
            Dispose(settings);
        }
    }
}
