// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.account;
using TLDownloadPreset = Ferrite.TL.baseLayer.TLAutoDownloadSettings;
using DownloadPreset = Ferrite.TL.baseLayer.AutoDownloadSettings;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class SaveAutoDownloadSettingsHandler : AccountSettingsHandlerBase
{
    public SaveAutoDownloadSettingsHandler(AccountSettingsStore store) : base(store) { }

    [TLFunction(Constructors.baseLayer_SaveAutoDownloadSettings)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await GetUserIdAsync(authKeyId);
        if (!userId.HasValue) return AuthError();
        var request = new SaveAutoDownloadSettings(q.AsSpan());
        using TLDownloadPreset settings = ((DownloadPreset)request.Settings)
            .Clone().Build();
        return await Store.SaveAutoDownloadAsync(userId.Value, request.Low,
            request.High, settings);
    }
}
