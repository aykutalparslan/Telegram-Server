// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.TL;
using Ferrite.TL.baseLayer.dto;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

public sealed class GetGroupsForDiscussionHandler : ChannelCatalogueHandlerBase
{
    private readonly IChannelAdminRepository _channelAdminRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;

    public GetGroupsForDiscussionHandler(IUnitOfWork unitOfWork, IChannelAdminRepository channelAdminRepository, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,
        ICounterFactory counterFactory, IdAllocators ids,
        IUpdatesContextFactory updatesContextFactory, IUpdatesService updates,
        ISearchEngine search, IUploadService upload,
        IPhotoProcessingService photos, ILogger log, ChatRowStore chatRows,
        UpdateFanout fanout)
        : base(unitOfWork, authorizationRepository, channelAdminLogRepository, channelMessagesRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, counterFactory, ids, updatesContextFactory, updates,
            search, upload, photos, log, chatRows, fanout)
    {
        _channelAdminRepository = channelAdminRepository;
        _chatParticipantsRepository = chatParticipantsRepository;

    }

    [TLFunction(Constructors.baseLayer_GetGroupsForDiscussion)]
    public async Task<Ferrite.TL.baseLayer.messages.TLChats> Handle(long authKeyId,
        TLBytes q)
    {
        long? callerUserId = await ResolveCallerAsync(authKeyId);
        if (callerUserId is null)
        {
            return ErrorChats("AUTH_KEY_INVALID"u8);
        }

        List<ChannelMembership> membership = await ReadMembershipAsync(callerUserId.Value);
        var selected = new List<long>();
        foreach (ChannelMembership channel in membership)
        {
            if (!channel.Megagroup || channel.Gigagroup || !IsActiveRole(channel.Role))
            {
                continue;
            }
            if (!await CanPinAsync(channel.ChannelId, callerUserId.Value))
            {
                continue;
            }

            using TLChannelAdminState? state = await _channelAdminRepository.GetStateAsync(channel.ChannelId);
            if (state is { } stored)
            {
                var view = stored.AsChannelAdminState();
                if (view.LinkedChatId != 0 || view.HiddenPrehistory)
                {
                    continue;
                }
            }

            selected.Add(channel.ChannelId);
        }

        _log.Debug($"📣 GetGroupsForDiscussion user:{callerUserId.Value} " +
                   $"count:{selected.Count}");
        return await BuildChatsAsync(callerUserId.Value, selected);
    }

    private async Task<bool> CanPinAsync(long channelId, long userId)
    {
        using TLChatParticipantInfo? participant = await _chatParticipantsRepository.GetParticipantAsync(channelId, userId);
        return participant is { } stored &&
               ChatRights.HasAdminRight(stored, ChatAdminRightRequirement.PinMessages);
    }
}
