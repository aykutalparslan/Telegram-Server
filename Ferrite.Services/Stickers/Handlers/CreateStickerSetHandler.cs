// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.stickers;

namespace Ferrite.Services.Handlers.StickerMethods;

public sealed class CreateStickerSetHandler : StickerHandlerBase
{
    public CreateStickerSetHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, StickerStore store)
        : base(unitOfWork, authorizationRepository, store) { }

    [TLFunction(Constructors.baseLayer_CreateStickerSet)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var request = (CreateStickerSet)q;
        InputUserView requestedOwner = request.Get_UserIdView();
        bool isSelf = requestedOwner.Is(out InputUserSelf _);
        long? explicitOwner = requestedOwner.Is(out InputUser user)
            ? user.UserId : null;
        string title = Encoding.UTF8.GetString(request.Title);
        string shortName = Encoding.UTF8.GetString(request.ShortName);
        bool masks = request.Masks;
        bool emojis = request.Emojis;
        bool textColor = request.TextColor;
        var items = new List<StickerStore.StickerItemInput>();
        Vector source = request.Stickers;
        int count = source.Count;
        for (int i = 0; i < count; i++)
        {
            StickerStore.StickerItemInput? item = StickerStore.ReadItem(
                (InputStickerSetItemView)source.ReadTLObject());
            if (!item.HasValue) return Invalid("STICKER_INVALID");
            items.Add(item.Value);
        }
        (long Id, long AccessHash)? thumb = null;
        if (request.Flags[2])
        {
            var value = StickerStore.ReadInputDocument(request.Get_ThumbView());
            if (!value.Id.HasValue || !value.AccessHash.HasValue)
                return Invalid("STICKER_ID_INVALID");
            thumb = (value.Id.Value, value.AccessHash.Value);
        }
        if (masks && emojis) return Invalid("STICKERSET_INVALID");
        long? ownerUserId = await GetUserIdAsync(authKeyId);
        if (!ownerUserId.HasValue) return AuthError();
        if (!isSelf && explicitOwner != ownerUserId.Value)
            return Invalid("USER_ID_INVALID");
        StickerSetKind kind = masks ? StickerSetKind.Mask
            : emojis ? StickerSetKind.Emoji : StickerSetKind.Regular;
        return await Store.CreateSetAsync(ownerUserId.Value, title, shortName,
            kind, textColor, items, thumb);
    }
}
