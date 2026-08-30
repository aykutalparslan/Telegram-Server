// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.account;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class UploadWallPaperHandler : WallpaperHandlerBase
{
    public UploadWallPaperHandler(WallpaperStore store, ProfileStore profiles)
        : base(store, profiles) { }

    [TLFunction(Constructors.layer95_AccountUploadWallPaper)]
    public async Task<TLBytes> HandleLayer95(long authKeyId, TLBytes q)
    {
        using var current = ToCurrentUploadWallPaperRequest(q);
        return await Handle(authKeyId, current);
    }

    private static TLBytes ToCurrentUploadWallPaperRequest(TLBytes q)
    {
        var sent = new TL.layer95.account.AccountUploadWallPaper(q.AsSpan());
        var current = UploadWallPaper.Builder()
            .File(sent.File)
            .MimeType(sent.MimeType)
            .Settings(sent.Settings)
            .Build();
        return current.TLBytes!.Value;
    }

    [TLFunction(Constructors.baseLayer_UploadWallPaper)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await GetUserIdAsync(authKeyId);
        if (!userId.HasValue) return AuthError();
        var request = new UploadWallPaper(q.AsSpan());
        string mime = Encoding.UTF8.GetString(request.MimeType).ToLowerInvariant();
        if (mime is not ("image/jpeg" or "image/png") ||
            !ChatPhotos.TryReadInputFile(request.Get_FileView(), out var file))
            return Invalid();
        using TLWallPaperSettings? settings = CloneSettings(
            request.Get_SettingsView());
        if (settings is null) return Invalid();
        using (file)
        {
            return await Store.UploadAsync(userId.Value, file,
                Encoding.UTF8.GetBytes(mime), settings.Value);
        }
    }
}
