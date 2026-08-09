// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer.channels;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

public sealed class SetStickersHandler : ChannelStickerHandlerBase
{
    public SetStickersHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IAuthorizationRepository authorizationRepository, IChatRepository chatRepository, IStickerRepository stickerRepository, UpdateFanout fanout,
        ILogger log) : base(unitOfWork, chatParticipantsRepository, authorizationRepository, chatRepository, stickerRepository, fanout, log) { }

    [TLFunction(Constructors.baseLayer_SetStickers)]
    public async Task<TLBool> Handle(long authKeyId, TLBytes q)
    {
        var request = (SetStickers)q;
        long? channelId = ReadChannel(request.Get_ChannelView());
        StickerSetInput set = ReadSet(request.Get_StickersetView());
        return await SetAsync(authKeyId, channelId, set, emoji: false);
    }
}
