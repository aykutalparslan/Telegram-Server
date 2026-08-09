// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.account;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class UploadThemeHandler : ThemeHandlerBase
{
    public UploadThemeHandler(ThemeStore store, ProfileStore profiles)
        : base(store, profiles) { }

    [TLFunction(Constructors.baseLayer_UploadTheme)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await GetUserIdAsync(authKeyId);
        if (!userId.HasValue) return AuthError();
        var request = new UploadTheme(q.AsSpan());
        string fileName = Encoding.UTF8.GetString(request.FileName).Trim();
        string mimeType = Encoding.UTF8.GetString(request.MimeType).Trim()
            .ToLowerInvariant();
        if (fileName.Length is 0 or > 255 || mimeType.Length is 0 or > 128 ||
            !ChatPhotos.TryReadInputFile(request.Get_FileView(), out var file))
            return Invalid();
        TLInputFile? thumb = null;
        if (request.Flags[0])
        {
            if (!ChatPhotos.TryReadInputFile(request.Get_ThumbView(), out var value))
            {
                file.Dispose();
                return Invalid();
            }
            thumb = value;
        }
        try
        {
            return await Store.UploadAsync(file, thumb, fileName, mimeType);
        }
        finally
        {
            file.Dispose();
            thumb?.Dispose();
        }
    }
}
