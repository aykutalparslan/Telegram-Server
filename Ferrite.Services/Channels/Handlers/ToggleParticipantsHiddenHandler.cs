// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.Services.Channels;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.channels;
using Ferrite.TL.baseLayer.dto;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

public sealed class ToggleParticipantsHiddenHandler : ChannelPropertyHandlerBase
{
    private readonly IChannelAdminRepository _channelAdminRepository;

    public ToggleParticipantsHiddenHandler(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChannelAdminRepository channelAdminRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,
        ICounterFactory counterFactory, IdAllocators ids,
        IUpdatesContextFactory updatesContextFactory, IUpdatesService updates,
        ISearchEngine search, IUploadService upload,
        IPhotoProcessingService photos, ILogger log, ChatRowStore chatRows,
        UpdateFanout fanout)
        : base(unitOfWork, channelMessagesRepository, authorizationRepository, channelAdminLogRepository, channelAdminRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, counterFactory, ids, updatesContextFactory, updates,
            search, upload, photos, log, chatRows, fanout)
    {
        _channelAdminRepository = channelAdminRepository;

    }

    [TLFunction(Constructors.baseLayer_ToggleParticipantsHidden)]
    public async Task<Ferrite.TL.baseLayer.TLUpdates> Handle(long authKeyId, TLBytes q)
    {
        var request = (ToggleParticipantsHidden)q;
        long? channelId = ResolveInputChannelId(request.Get_ChannelView());
        bool enabled = request.Enabled;

        var (currentUserId, channelBytes, error) = await PrepareChannelMutation(
            authKeyId, channelId, creatorOnly: false,
            ChatAdminRightRequirement.BanUsers);
        if (error != null)
        {
            return error.Value;
        }

        if (!ReadChannelFacts(channelBytes).Megagroup)
        {
            return ErrorUpdates("MEGAGROUP_REQUIRED"u8);
        }

        long id = channelId!.Value;
        int date = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
        using (TLChannelAdminState state = await LoadAdminStateAsync(id, date))
        {
            var view = state.AsChannelAdminState();
            if (view.ParticipantsHidden == enabled)
            {
                return ErrorUpdates("CHAT_NOT_MODIFIED"u8);
            }

            Flags flags = view.Flags;
            flags[1] = enabled;
            using TLChannelAdminState updated =
                ChannelAdminStateRows.WithFlags(view, flags, date);
            _channelAdminRepository.PutState(updated);
        }

        _log.Debug($"📣 ToggleParticipantsHidden user:{currentUserId} channel:{id} enabled:{enabled}");
        return await CompleteAsync(authKeyId, currentUserId, id, channelBytes);
    }
}
