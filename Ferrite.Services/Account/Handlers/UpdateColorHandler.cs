// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.account;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class UpdateColorHandler : ProfileSettingsHandlerBase
{
    public UpdateColorHandler(ProfileStore store) : base(store) { }

    [TLFunction(Constructors.baseLayer_AccountUpdateColor)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await GetUserIdAsync(authKeyId);
        if (!userId.HasValue) return AuthError();
        var request = new AccountUpdateColor(q.AsSpan());
        if (request.Flags[2] && request.Color < 0)
            return Invalid("COLOR_INVALID"u8);
        if (request.Flags[0] && request.BackgroundEmojiId <= 0)
            return Invalid("EMOJI_ID_INVALID"u8);
        TLPeerColor? color = null;
        if (request.Flags[2] || request.Flags[0])
        {
            var builder = PeerColor.Builder();
            if (request.Flags[2]) builder = builder.Color(request.Color);
            if (request.Flags[0])
                builder = builder.BackgroundEmojiId(request.BackgroundEmojiId);
            color = builder.Build();
        }
        using (color)
            return await Store.UpdateColorAsync(userId.Value,
                request.ForProfile, color);
    }
}
