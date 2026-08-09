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
/// How many boosts lift a supergroup's default restrictions for a member.
/// `set_channel_unrestrict_boost_count` (`ChatManager.cpp:3287-3300`) refuses a
/// broadcast channel, requires restrict-members rights, and bounds the count to
/// 0..8; the server enforces the same bound rather than trusting the client's.
/// </summary>
public sealed class SetBoostsToUnblockRestrictionsHandler : ChannelPropertyHandlerBase
{
    private readonly IChannelAdminRepository _channelAdminRepository;

    private const int MaxBoosts = 8;

    public SetBoostsToUnblockRestrictionsHandler(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChannelAdminRepository channelAdminRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,
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

    [TLFunction(Constructors.baseLayer_SetBoostsToUnblockRestrictions)]
    public async Task<Ferrite.TL.baseLayer.TLUpdates> Handle(long authKeyId, TLBytes q)
    {
        var request = (SetBoostsToUnblockRestrictions)q;
        long? channelId = ResolveInputChannelId(request.Get_ChannelView());
        int boosts = request.Boosts;

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
        if (boosts < 0 || boosts > MaxBoosts)
        {
            return ErrorUpdates("BOOSTS_INVALID"u8);
        }

        long id = channelId!.Value;
        int date = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
        using (TLChannelAdminState state = await LoadAdminStateAsync(id, date))
        {
            var view = state.AsChannelAdminState();
            if (view.BoostsUnrestrict == boosts)
            {
                return ErrorUpdates("CHAT_NOT_MODIFIED"u8);
            }

            using TLChannelAdminState updated =
                ChannelAdminStateRows.WithBoostsUnrestrict(view, boosts, date);
            _channelAdminRepository.PutState(updated);
        }

        _log.Debug($"📣 SetBoostsToUnblockRestrictions user:{currentUserId} channel:{id} boosts:{boosts}");
        return await CompleteAsync(authKeyId, currentUserId, id, channelBytes);
    }
}
