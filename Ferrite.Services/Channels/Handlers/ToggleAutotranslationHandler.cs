// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.Services.Channels;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.channels;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

/// <summary>
/// Automatic translation of a broadcast channel's posts.
/// `toggle_channel_has_automatic_translation` (`ChatManager.cpp:3394-3405`)
/// refuses anything that is not a channel and needs change-info rights.
/// `autotranslation` is `flags2.15` on the compact `channel` row.
/// </summary>
public sealed class ToggleAutotranslationHandler : ChannelPropertyHandlerBase
{
    public ToggleAutotranslationHandler(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChannelAdminRepository channelAdminRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,
        ICounterFactory counterFactory, IdAllocators ids,
        IUpdatesContextFactory updatesContextFactory, IUpdatesService updates,
        ISearchEngine search, IUploadService upload,
        IPhotoProcessingService photos, ILogger log, ChatRowStore chatRows,
        UpdateFanout fanout)
        : base(unitOfWork, channelMessagesRepository, authorizationRepository, channelAdminLogRepository, channelAdminRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, counterFactory, ids, updatesContextFactory, updates,
            search, upload, photos, log, chatRows, fanout)
    {
    }

    [TLFunction(Constructors.baseLayer_ToggleAutotranslation)]
    public async Task<Ferrite.TL.baseLayer.TLUpdates> Handle(long authKeyId, TLBytes q)
    {
        var request = (ToggleAutotranslation)q;
        long? channelId = ResolveInputChannelId(request.Get_ChannelView());
        bool enabled = request.Enabled;

        var (currentUserId, channelBytes, error) = await PrepareChannelMutation(
            authKeyId, channelId, creatorOnly: false,
            ChatAdminRightRequirement.ChangeInfo);
        if (error != null)
        {
            return error.Value;
        }

        ChannelFacts facts = ReadChannelFacts(channelBytes);
        if (facts.Megagroup)
        {
            return ErrorUpdates("BROADCAST_REQUIRED"u8);
        }
        if (facts.Flags2[15] == enabled)
        {
            return ErrorUpdates("CHAT_NOT_MODIFIED"u8);
        }

        long id = channelId!.Value;
        byte[] updatedChannelBytes = StoreChannelFlags(channelBytes,
            flags2Bit: 15, flags2Value: enabled);
        int date = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
        await AppendAdminLogEventAsync(id, currentUserId,
            ChannelAdminLogRows.BoolAction(
                ChannelAdminLogRows.BoolActionKind.Autotranslation, enabled), date);

        _log.Debug($"📣 ToggleAutotranslation user:{currentUserId} channel:{id} enabled:{enabled}");
        return await CompleteAsync(authKeyId, currentUserId, id, updatedChannelBytes);
    }
}
