// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.TL;
using Ferrite.TL.baseLayer.channels;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

/// <summary>
/// The channels the caller has LEFT but whose participant row still exists, so
/// a client can offer to rejoin or forget them.
///
/// Pinned TDLib issues this method from NOWHERE: there is no
/// `telegram_api::channels_getLeftChannels` call site in the pinned tree, no
/// query class and no td_api entry point, so the generated-request Function/RPC
/// gate is its only real integration by construction.
///
/// A BANNED channel is not a left one. Both roles put the caller outside the
/// channel, but only leaving is the caller's own decision, and offering to
/// rejoin a channel that ejected the caller would be a lie.
/// </summary>
public sealed class GetLeftChannelsHandler : ChannelCatalogueHandlerBase
{
    // Matches the page size Telegram's own catalogue answers with; `offset` is
    // an index into the ordered list rather than an id cursor.
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
