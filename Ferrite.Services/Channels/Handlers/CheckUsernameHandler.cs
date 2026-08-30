// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using System.Text.RegularExpressions;
using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.channels;
using Ferrite.TL.baseLayer.dto;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

public sealed class CheckUsernameHandler : ChannelsHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;

    public CheckUsernameHandler(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository, ICounterFactory counterFactory,
        IdAllocators ids, IUpdatesContextFactory updatesContextFactory,
        IUpdatesService updates, ISearchEngine search, IUploadService upload,
        IPhotoProcessingService photos, ILogger log, ChatRowStore chatRows,
        UpdateFanout fanout)
        : base(unitOfWork, channelMessagesRepository, authorizationRepository, channelAdminLogRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, counterFactory, ids, updatesContextFactory, updates,
            search, upload, photos, log, chatRows, fanout)
    {
        _authorizationRepository = authorizationRepository;

    }

    [TLFunction(Constructors.baseLayer_ChannelsCheckUsername)]
    public async Task<TLBool> Handle(long authKeyId, TLBytes q)
    {
        var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
        if (auth == null)
        {
            return ErrorBool("AUTH_KEY_INVALID"u8);
        }

        var request = (ChannelsCheckUsername)q;
        long? channelId = ResolveInputChannelId(request.Get_ChannelView());
        string username = Encoding.UTF8.GetString(request.Username);
        if (!UsernameRegex.IsMatch(username))
        {
            return ErrorBool("USERNAME_INVALID"u8);
        }

        bool occupied = IsUsernameOccupied(username, channelId);
        _log.Debug($"📣 CheckUsername user:{auth.Value.AsAuthInfo().UserId} " +
                   $"channel:{channelId?.ToString() ?? "-"} username:{username} occupied:{occupied}");
        return occupied ? new BoolFalse() : new BoolTrue();
    }
}
