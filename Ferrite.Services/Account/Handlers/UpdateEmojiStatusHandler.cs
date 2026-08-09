// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.account;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class UpdateEmojiStatusHandler : ProfileSettingsHandlerBase
{
    public UpdateEmojiStatusHandler(ProfileStore store) : base(store) { }

    [TLFunction(Constructors.baseLayer_AccountUpdateEmojiStatus)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await GetUserIdAsync(authKeyId);
        if (!userId.HasValue) return AuthError();
        EmojiStatusView view = new AccountUpdateEmojiStatus(q.AsSpan())
            .Get_EmojiStatusView();
        using TLEmojiStatus? status = Clone(view);
        if (status is null) return Invalid("EMOJI_STATUS_INVALID"u8);
        return await Store.UpdateEmojiStatusAsync(userId.Value, status.Value);
    }

    private static TLEmojiStatus? Clone(EmojiStatusView view)
    {
        if (view.Is(out EmojiStatusEmpty empty)) return empty.Clone().Build();
        if (view.Is(out EmojiStatus value)) return value.Clone().Build();
        if (view.Is(out EmojiStatusCollectible collectible))
            return collectible.Clone().Build();
        return null;
    }
}
