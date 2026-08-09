// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL;
using Ferrite.TL.baseLayer.account;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class UploadRingtoneHandler : AccountAudioHandlerBase
{
    public UploadRingtoneHandler(AccountAudioStore store, ProfileStore profiles)
        : base(store, profiles) { }

    [TLFunction(Constructors.baseLayer_UploadRingtone)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await GetUserIdAsync(authKeyId);
        if (!userId.HasValue) return AuthError();
        var request = new UploadRingtone(q.AsSpan());
        string fileName = Encoding.UTF8.GetString(request.FileName).Trim();
        string mimeType = Encoding.UTF8.GetString(request.MimeType).Trim()
            .ToLowerInvariant();
        if (fileName.Length is 0 or > 255 ||
            !ChatPhotos.TryReadInputFile(request.Get_FileView(), out var file))
            return DocumentError();
        using (file)
        {
            return await Store.UploadRingtoneAsync(userId.Value, authKeyId, file,
                fileName, mimeType);
        }
    }
}
