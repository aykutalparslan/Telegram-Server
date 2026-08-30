// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer;

namespace Ferrite.Services.Stickers;

public sealed class StickerUpdateNotifier
{
    private readonly IUpdatesService _updates;

    public StickerUpdateNotifier(IUpdatesService updates)
    {
        _updates = updates;
    }

    public async ValueTask NotifySetsAsync(long userId, long authKeyId,
        StickerSetKind kind)
    {
        var builder = UpdateStickerSets.Builder();
        if (kind == StickerSetKind.Mask) builder = builder.Masks(true);
        if (kind == StickerSetKind.Emoji) builder = builder.Emojis(true);
        using TLUpdate update = builder.Build();
        await EnqueueAsync(userId, authKeyId, update);
    }

    public async ValueTask NotifyCollectionAsync(long userId, long authKeyId,
        StickerCollection collection)
    {
        using TLUpdate update = collection switch
        {
            StickerCollection.SavedGifs => UpdateSavedGifs.Builder().Build(),
            StickerCollection.Faved => UpdateFavedStickers.Builder().Build(),
            _ => UpdateRecentStickers.Builder().Build(),
        };
        await EnqueueAsync(userId, authKeyId, update);
    }

    public async ValueTask NotifyFeaturedReadAsync(long userId, long authKeyId)
    {
        using TLUpdate update = UpdateReadFeaturedStickers.Builder().Build();
        await EnqueueAsync(userId, authKeyId, update);
    }

    private async ValueTask EnqueueAsync(long userId, long authKeyId,
        TLUpdate update) =>
        await _updates.EnqueueUpdate(userId, update,
            UpdateDeliveryScope.ExcludingAuthKeys([authKeyId]));
}
