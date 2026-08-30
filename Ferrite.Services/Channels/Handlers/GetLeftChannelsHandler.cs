// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.TL;
using Ferrite.TL.baseLayer.channels;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

public sealed class GetLeftChannelsHandler : ChannelCatalogueHandlerBase
{
    private const int PageSize = 100;

    public GetLeftChannelsHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,
        ICounterFactory counterFactory, IdAllocators ids,
        IUpdatesContextFactory updatesContextFactory, IUpdatesService updates,
        ISearchEngine search, IUploadService upload,
        IPhotoProcessingService photos, ILogger log, ChatRowStore chatRows,
        UpdateFanout fanout)
        : base(unitOfWork, authorizationRepository, channelAdminLogRepository, channelMessagesRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, counterFactory, ids, updatesContextFactory, updates,
            search, upload, photos, log, chatRows, fanout)
    {
    }

    [TLFunction(Constructors.baseLayer_GetLeftChannels)]
    public async Task<Ferrite.TL.baseLayer.messages.TLChats> Handle(long authKeyId,
        TLBytes q)
    {
        int offset = ((GetLeftChannels)q).Offset;
        if (offset < 0)
        {
            return ErrorChats("OFFSET_INVALID"u8);
        }

        long? callerUserId = await ResolveCallerAsync(authKeyId);
        if (callerUserId is null)
        {
            return ErrorChats("AUTH_KEY_INVALID"u8);
        }

        List<ChannelMembership> membership = await ReadMembershipAsync(callerUserId.Value);
        List<long> selected = membership
            .Where(x => x.Role == (int)ChatParticipantRole.Left)
            .Select(x => x.ChannelId)
            .Skip(offset)
            .Take(PageSize)
            .ToList();

        _log.Debug($"📣 GetLeftChannels user:{callerUserId.Value} offset:{offset} " +
                   $"count:{selected.Count}");
        return await BuildChatsAsync(callerUserId.Value, selected);
    }
}
