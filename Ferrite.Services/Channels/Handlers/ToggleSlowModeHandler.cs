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
/// Slow mode spans both rows: `slowmode_enabled` is a flag on the compact
/// `channel` row that every reader is served, while the delay itself has no home
/// there and lives in `dto.channelAdminState.slowmode_seconds`.
///
/// Turning it OFF drops the whole channel's per-user deadlines rather than
/// leaving them to expire: with no delay the next send is immediate for
/// EVERYONE, not merely for whoever had not posted yet.
///
/// `set_channel_slow_mode_delay` (`ChatManager.cpp:3671-3696`) accepts only
/// {0, 10, 30, 60, 300, 900, 3600}, requires a supergroup and gates it on
/// restrict-members rights.
/// </summary>
public sealed class ToggleSlowModeHandler : ChannelPropertyHandlerBase
{
    private readonly IChannelAdminRepository _channelAdminRepository;

    private static readonly int[] AllowedSeconds = [0, 10, 30, 60, 300, 900, 3600];

    public ToggleSlowModeHandler(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChannelAdminRepository channelAdminRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,
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

    [TLFunction(Constructors.baseLayer_ToggleSlowMode)]
    public async Task<Ferrite.TL.baseLayer.TLUpdates> Handle(long authKeyId, TLBytes q)
    {
        var request = (ToggleSlowMode)q;
        long? channelId = ResolveInputChannelId(request.Get_ChannelView());
        int seconds = request.Seconds;

        var (currentUserId, channelBytes, error) = await PrepareChannelMutation(
            authKeyId, channelId, creatorOnly: false,
            ChatAdminRightRequirement.BanUsers);
        if (error != null)
        {
            return error.Value;
        }

        ChannelFacts facts = ReadChannelFacts(channelBytes);
        if (!facts.Megagroup)
        {
            return ErrorUpdates("MEGAGROUP_REQUIRED"u8);
        }
        if (!AllowedSeconds.Contains(seconds))
        {
            return ErrorUpdates("SECONDS_INVALID"u8);
        }

        long id = channelId!.Value;
        int date = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
        bool enabled = seconds > 0;
        int previousSeconds;
        using (TLChannelAdminState state = await LoadAdminStateAsync(id, date))
        {
            var view = state.AsChannelAdminState();
            if (view.SlowmodeSeconds == seconds && facts.Flags[22] == enabled)
            {
                return ErrorUpdates("CHAT_NOT_MODIFIED"u8);
            }

            previousSeconds = view.SlowmodeSeconds;
            using TLChannelAdminState updated =
                ChannelAdminStateRows.WithSlowModeSeconds(view, seconds, date);
            _channelAdminRepository.PutState(updated);
        }
        if (!enabled)
        {
            await _channelAdminRepository.DeleteSlowModeStatesAsync(id);
        }

        byte[] updatedChannelBytes = StoreChannelFlags(channelBytes,
            flagBit: 22, flagValue: enabled);
        byte[] logAction;
        using (TLChannelAdminLogEventAction action =
               ChannelAdminLogEventActionToggleSlowMode.Builder()
                   .PrevValue(previousSeconds)
                   .NewValue(seconds)
                   .Build())
        {
            logAction = action.AsSpan().ToArray();
        }
        await AppendAdminLogEventAsync(id, currentUserId, logAction, date);

        _log.Debug($"📣 ToggleSlowMode user:{currentUserId} channel:{id} seconds:{seconds}");
        return await CompleteAsync(authKeyId, currentUserId, id, updatedChannelBytes);
    }
}
