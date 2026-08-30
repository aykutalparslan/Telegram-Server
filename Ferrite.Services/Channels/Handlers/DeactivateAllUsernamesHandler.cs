// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.Services.Channels;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.channels;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

public sealed class DeactivateAllUsernamesHandler : ChannelUsernameHandlerBase
{
    public DeactivateAllUsernamesHandler(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,
        ICounterFactory counterFactory, IdAllocators ids,
        IUpdatesContextFactory updatesContextFactory, IUpdatesService updates,
        ISearchEngine search, IUploadService upload,
        IPhotoProcessingService photos, ILogger log, ChatRowStore chatRows,
        UpdateFanout fanout)
        : base(unitOfWork, channelMessagesRepository, authorizationRepository, channelAdminLogRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, counterFactory, ids, updatesContextFactory, updates,
            search, upload, photos, log, chatRows, fanout)
    {
    }

    [TLFunction(Constructors.baseLayer_DeactivateAllUsernames)]
    public async Task<TLBool> Handle(long authKeyId, TLBytes q)
    {
        var request = (DeactivateAllUsernames)q;
        long? channelId = ResolveInputChannelId(request.Get_ChannelView());

        var (currentUserId, channelBytes, error) =
            await PrepareChannelMutationCore(authKeyId, channelId, creatorOnly: true);
        if (error != null)
        {
            return ErrorBool(Encoding.UTF8.GetBytes(error));
        }

        List<ChannelUsername> usernames = ReadUsernames(channelBytes);
        var deactivated = new List<ChannelUsername>(usernames.Count);
        bool changed = false;
        foreach (ChannelUsername username in usernames)
        {
            if (username.Editable || !username.Active)
            {
                deactivated.Add(username);
                continue;
            }

            deactivated.Add(username with { Active = false });
            changed = true;
        }

        if (!changed)
        {
            return ErrorBool("USERNAME_NOT_MODIFIED"u8);
        }

        long id = channelId!.Value;
        _log.Debug($"📣 DeactivateAllUsernames user:{currentUserId} channel:{id} " +
                   $"deactivated:{deactivated.Count(x => !x.Active)}");
        return await ApplyUsernamesAsync(currentUserId, id, channelBytes, deactivated);
    }
}
