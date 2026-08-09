// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.account;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class UpdatePersonalChannelHandler : ProfileSettingsHandlerBase
{
    private readonly IChatRepository _chatRepository;

    private readonly IChatRepository _chats;

    public UpdatePersonalChannelHandler(ProfileStore store, IUnitOfWork unitOfWork, IChatRepository chatRepository)
        : base(store) {
        _chatRepository = chatRepository;
        _chats = chatRepository;
    }

    [TLFunction(Constructors.baseLayer_UpdatePersonalChannel)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await GetUserIdAsync(authKeyId);
        if (!userId.HasValue) return AuthError();
        InputChannelView input = new UpdatePersonalChannel(q.AsSpan())
            .Get_ChannelView();
        if (input.Is(out InputChannelEmpty _))
            return await Store.UpdatePersonalChannelAsync(userId.Value, null);
        if (!input.Is(out InputChannel channel))
            return Invalid("CHANNEL_INVALID"u8);
        long channelId = channel.ChannelId;
        using TLChat? stored = await _chats.GetChatAsync(channelId);
        if (stored is null || stored.Value.Type != TLChat.ChatType.Channel)
            return Invalid("CHANNEL_INVALID"u8);
        return await Store.UpdatePersonalChannelAsync(userId.Value, channelId);
    }
}
