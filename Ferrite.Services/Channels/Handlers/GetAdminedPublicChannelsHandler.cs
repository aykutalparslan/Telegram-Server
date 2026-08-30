// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.TL;
using Ferrite.TL.baseLayer.channels;
using Ferrite.TL.baseLayer.dto;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

public sealed class GetAdminedPublicChannelsHandler : ChannelCatalogueHandlerBase
{
    private readonly IChannelAdminRepository _channelAdminRepository;

    public GetAdminedPublicChannelsHandler(IUnitOfWork unitOfWork, IChannelAdminRepository channelAdminRepository, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,
        ICounterFactory counterFactory, IdAllocators ids,
        IUpdatesContextFactory updatesContextFactory, IUpdatesService updates,
        ISearchEngine search, IUploadService upload,
        IPhotoProcessingService photos, ILogger log, ChatRowStore chatRows,
        UpdateFanout fanout)
        : base(unitOfWork, authorizationRepository, channelAdminLogRepository, channelMessagesRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, counterFactory, ids, updatesContextFactory, updates,
            search, upload, photos, log, chatRows, fanout)
    {
        _channelAdminRepository = channelAdminRepository;

    }

    [TLFunction(Constructors.baseLayer_GetAdminedPublicChannels)]
    public async Task<Ferrite.TL.baseLayer.messages.TLChats> Handle(long authKeyId,
        TLBytes q)
    {
        var request = (GetAdminedPublicChannels)q;
        bool byLocation = request.ByLocation;
        bool forPersonal = request.ForPersonal;

        long? callerUserId = await ResolveCallerAsync(authKeyId);
        if (callerUserId is null)
        {
            return ErrorChats("AUTH_KEY_INVALID"u8);
        }

        List<ChannelMembership> membership = await ReadMembershipAsync(callerUserId.Value);
        var selected = new List<long>();
        foreach (ChannelMembership channel in membership)
        {
            if (channel.Role != (int)ChatParticipantRole.Creator ||
                !channel.HasActiveUsername)
            {
                continue;
            }
            if (forPersonal && !channel.Broadcast)
            {
                continue;
            }
            if (byLocation && !await HasLocationAsync(channel.ChannelId))
            {
                continue;
            }

            selected.Add(channel.ChannelId);
        }

        _log.Debug($"📣 GetAdminedPublicChannels user:{callerUserId.Value} " +
                   $"count:{selected.Count} byLocation:{byLocation} personal:{forPersonal}");
        return await BuildChatsAsync(callerUserId.Value, selected);
    }

    private async Task<bool> HasLocationAsync(long channelId)
    {
        using TLChannelAdminState? state = await _channelAdminRepository.GetStateAsync(channelId);
        return state?.AsChannelAdminState().Flags[4] == true;
    }
}
