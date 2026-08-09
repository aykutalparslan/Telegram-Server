// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.channels;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

/// <summary>
/// Whether a user must join a supergroup before posting. `join_to_send` lives on
/// the compact `channel` row, so the stored row is what changes.
/// `toggle_channel_join_to_send` (`ChatManager.cpp:3326-3333`) restricts it to an
/// ordinary supergroup and gates it on restrict-members rights.
/// </summary>
public sealed class ToggleJoinToSendHandler : ChannelPropertyHandlerBase
{
    public ToggleJoinToSendHandler(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChannelAdminRepository channelAdminRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,
        ICounterFactory counterFactory, IdAllocators ids,
        IUpdatesContextFactory updatesContextFactory, IUpdatesService updates,
        ISearchEngine search, IUploadService upload,
        IPhotoProcessingService photos, ILogger log, ChatRowStore chatRows,
        UpdateFanout fanout)
        : base(unitOfWork, channelMessagesRepository, authorizationRepository, channelAdminLogRepository, channelAdminRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, counterFactory, ids, updatesContextFactory, updates,
            search, upload, photos, log, chatRows, fanout)
    {
    }

    [TLFunction(Constructors.baseLayer_ToggleJoinToSend)]
    public async Task<Ferrite.TL.baseLayer.TLUpdates> Handle(long authKeyId, TLBytes q)
    {
        var request = (ToggleJoinToSend)q;
        long? channelId = ResolveInputChannelId(request.Get_ChannelView());
        bool enabled = request.Enabled;

        var (currentUserId, channelBytes, error) = await PrepareChannelMutation(
            authKeyId, channelId, creatorOnly: false,
            ChatAdminRightRequirement.BanUsers);
        if (error != null)
        {
            return error.Value;
        }

        ChannelFacts facts = ReadChannelFacts(channelBytes);
        if (!facts.Megagroup || facts.Flags[26])
        {
            return ErrorUpdates("MEGAGROUP_REQUIRED"u8);
        }
        if (facts.Flags[28] == enabled)
        {
            return ErrorUpdates("CHAT_NOT_MODIFIED"u8);
        }

        long id = channelId!.Value;
        byte[] updatedChannelBytes = StoreChannelFlags(channelBytes,
            flagBit: 28, flagValue: enabled);
        _log.Debug($"📣 ToggleJoinToSend user:{currentUserId} channel:{id} enabled:{enabled}");
        return await CompleteAsync(authKeyId, currentUserId, id, updatedChannelBytes);
    }
}
