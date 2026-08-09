// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.Services.Channels;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.channels;
using Ferrite.TL.baseLayer.dto;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

/// <summary>
/// Aggressive anti-spam is a supergroup-only setting and is refused on a
/// broadcast group, matching `can_toggle_channel_aggressive_anti_spam`
/// (`ChatManager.cpp:3468-3482`), which also gates it on delete-messages rather
/// than change-info rights.
/// </summary>
public sealed class ToggleAntiSpamHandler : ChannelPropertyHandlerBase
{
    private readonly IChannelAdminRepository _channelAdminRepository;

    public ToggleAntiSpamHandler(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChannelAdminRepository channelAdminRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,
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

    [TLFunction(Constructors.baseLayer_ToggleAntiSpam)]
    public async Task<Ferrite.TL.baseLayer.TLUpdates> Handle(long authKeyId, TLBytes q)
    {
        var request = (ToggleAntiSpam)q;
        long? channelId = ResolveInputChannelId(request.Get_ChannelView());
        bool enabled = request.Enabled;

        var (currentUserId, channelBytes, error) = await PrepareChannelMutation(
            authKeyId, channelId, creatorOnly: false,
            ChatAdminRightRequirement.DeleteMessages);
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
            return ErrorUpdates("GIGAGROUP_INVALID"u8);
        }

        long id = channelId!.Value;
        int date = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
        using (TLChannelAdminState state = await LoadAdminStateAsync(id, date))
        {
            var view = state.AsChannelAdminState();
            if (view.Antispam == enabled)
            {
                return ErrorUpdates("CHAT_NOT_MODIFIED"u8);
            }

            Flags flags = view.Flags;
            flags[0] = enabled;
            using TLChannelAdminState updated =
                ChannelAdminStateRows.WithFlags(view, flags, date);
            _channelAdminRepository.PutState(updated);
        }

        await AppendAdminLogEventAsync(id, currentUserId,
            ChannelAdminLogRows.BoolAction(
                ChannelAdminLogRows.BoolActionKind.AntiSpam, enabled), date);

        _log.Debug($"📣 ToggleAntiSpam user:{currentUserId} channel:{id} enabled:{enabled}");
        return await CompleteAsync(authKeyId, currentUserId, id, channelBytes);
    }
}
