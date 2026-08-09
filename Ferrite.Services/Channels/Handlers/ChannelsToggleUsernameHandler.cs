// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
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
/// Activates or deactivates ONE of a channel's usernames, keeping the
/// reservation either way.
///
/// The editable username cannot be toggled. That is not a Ferrite restriction:
/// `Usernames::can_toggle` (`Usernames.cpp:89-97`) refuses it client-side, and
/// `Usernames::Usernames` (`Usernames.cpp:38-43`) logs an error and discards the
/// whole collection if the server ever answers with a disabled editable
/// username. Ferrite issues no fragment-purchased usernames, so a channel here
/// normally holds nothing BUT its editable username and the pinned-client route
/// is expected to be refused before it reaches the server.
/// </summary>
public sealed class ChannelsToggleUsernameHandler : ChannelUsernameHandlerBase
{
    public ChannelsToggleUsernameHandler(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,
        ICounterFactory counterFactory, IdAllocators ids,
        IUpdatesContextFactory updatesContextFactory, IUpdatesService updates,
        ISearchEngine search, IUploadService upload,
        IPhotoProcessingService photos, ILogger log, ChatRowStore chatRows,
        UpdateFanout fanout)
        : base(unitOfWork, channelMessagesRepository, authorizationRepository, channelAdminLogRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, counterFactory, ids, updatesContextFactory, updates,
            search, upload, photos, log, chatRows, fanout)
    {
    }

    [TLFunction(Constructors.baseLayer_ChannelsToggleUsername)]
    public async Task<TLBool> Handle(long authKeyId, TLBytes q)
    {
        var request = (ChannelsToggleUsername)q;
        long? channelId = ResolveInputChannelId(request.Get_ChannelView());
        string username = Encoding.UTF8.GetString(request.Username);
        bool active = request.Active;

        var (currentUserId, channelBytes, error) =
            await PrepareChannelMutationCore(authKeyId, channelId, creatorOnly: true);
        if (error != null)
        {
            return ErrorBool(Encoding.UTF8.GetBytes(error));
        }

        List<ChannelUsername> usernames = ReadUsernames(channelBytes);
        int index = usernames.FindIndex(x => string.Equals(x.Username, username,
            StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            return ErrorBool("USERNAME_NOT_OCCUPIED"u8);
        }
        if (usernames[index].Editable && !active)
        {
            return ErrorBool("USERNAME_INVALID"u8);
        }
        if (usernames[index].Active == active)
        {
            return ErrorBool("USERNAME_NOT_MODIFIED"u8);
        }

        usernames[index] = usernames[index] with { Active = active };
        long id = channelId!.Value;
        _log.Debug($"📣 ChannelsToggleUsername user:{currentUserId} channel:{id} " +
                   $"username:'{username}' active:{active}");
        return await ApplyUsernamesAsync(currentUserId, id, channelBytes, usernames);
    }
}
