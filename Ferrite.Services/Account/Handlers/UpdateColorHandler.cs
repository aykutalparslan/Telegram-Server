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
        if (!request.Flags[2])
        {
            return await Store.UpdateColorAsync(userId.Value,
                request.ForProfile, null);
        }

        var view = request.Get_ColorView();
        if (view.Is(out PeerColor peerColor))
        {
            if (peerColor.Flags[0] && peerColor.Color < 0)
                return Invalid("COLOR_INVALID"u8);
            if (peerColor.Flags[1] && peerColor.BackgroundEmojiId <= 0)
                return Invalid("EMOJI_ID_INVALID"u8);
        }

        using TLPeerColor color = request.Get_Color();
        return await Store.UpdateColorAsync(userId.Value,
            request.ForProfile, color);
    }
}
