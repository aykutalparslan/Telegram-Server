// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.channels;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

public sealed class ConvertToGigagroupHandler : ChannelPropertyHandlerBase
{
    public ConvertToGigagroupHandler(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChannelAdminRepository channelAdminRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,
        ICounterFactory counterFactory, IdAllocators ids,
        IUpdatesContextFactory updatesContextFactory, IUpdatesService updates,
        ISearchEngine search, IUploadService upload,
        IPhotoProcessingService photos, ILogger log, ChatRowStore chatRows,
        UpdateFanout fanout)
        : base(unitOfWork, channelMessagesRepository, authorizationRepository, channelAdminLogRepository, channelAdminRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, counterFactory, ids, updatesContextFactory, updates,
            search, upload, photos, log, chatRows, fanout)
    {
    }

    [TLFunction(Constructors.baseLayer_ConvertToGigagroup)]
    public async Task<Ferrite.TL.baseLayer.TLUpdates> Handle(long authKeyId, TLBytes q)
    {
        var request = (ConvertToGigagroup)q;
        long? channelId = ResolveInputChannelId(request.Get_ChannelView());

        var (currentUserId, channelBytes, error) = await PrepareChannelMutation(
            authKeyId, channelId, creatorOnly: true);
        if (error != null)
        {
            return error.Value;
        }

        ChannelFacts facts = ReadChannelFacts(channelBytes);
        if (!facts.Megagroup)
        {
            return ErrorUpdates("MEGAGROUP_REQUIRED"u8);
        }
        if (facts.Flags[26])
        {
            return ErrorUpdates("CHAT_NOT_MODIFIED"u8);
        }

        long id = channelId!.Value;
        byte[] updatedChannelBytes = StoreChannelFlags(channelBytes,
            flagBit: 26, flagValue: true);
        _log.Debug($"📣 ConvertToGigagroup user:{currentUserId} channel:{id}");
        return await CompleteAsync(authKeyId, currentUserId, id, updatedChannelBytes);
    }
}
