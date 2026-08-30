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

public sealed class TogglePreHistoryHiddenHandler : ChannelPropertyHandlerBase
{
    private readonly IChannelAdminRepository _channelAdminRepository;

    public TogglePreHistoryHiddenHandler(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChannelAdminRepository channelAdminRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,
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

    [TLFunction(Constructors.baseLayer_TogglePreHistoryHidden)]
    public async Task<Ferrite.TL.baseLayer.TLUpdates> Handle(long authKeyId, TLBytes q)
    {
        var request = (TogglePreHistoryHidden)q;
        long? channelId = ResolveInputChannelId(request.Get_ChannelView());
        bool hidden = request.Enabled;

        var (currentUserId, channelBytes, error) = await PrepareChannelMutation(
            authKeyId, channelId, creatorOnly: false,
            ChatAdminRightRequirement.ChangeInfo);
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
            if (view.HiddenPrehistory == hidden)
            {
                return ErrorUpdates("CHAT_NOT_MODIFIED"u8);
            }

            Flags flags = view.Flags;
            flags[2] = hidden;
            using TLChannelAdminState updated =
                ChannelAdminStateRows.WithFlags(view, flags, date);
            _channelAdminRepository.PutState(updated);
        }

        await AppendAdminLogEventAsync(id, currentUserId,
            ChannelAdminLogRows.BoolAction(
                ChannelAdminLogRows.BoolActionKind.PreHistoryHidden, hidden), date);

        _log.Debug($"📣 TogglePreHistoryHidden user:{currentUserId} channel:{id} hidden:{hidden}");
        return await CompleteAsync(authKeyId, currentUserId, id, channelBytes);
    }
}
