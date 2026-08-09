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
using Ferrite.TL.baseLayer.dto;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

/// <summary>
/// Stores the tab a channel's profile opens on, surfaced as
/// `channelFull.main_tab`. Pinned TDLib issues this method from NOWHERE -- it
/// has no query class and no td_api entry point, the same shape
/// `account.setMainProfileTab` had in -- so the generated-request
/// Function/RPC gate is its only real integration by construction.
/// </summary>
public sealed class ChannelsSetMainProfileTabHandler : ChannelPropertyHandlerBase
{
    private readonly IChannelAdminRepository _channelAdminRepository;

    public ChannelsSetMainProfileTabHandler(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChannelAdminRepository channelAdminRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,
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

    [TLFunction(Constructors.baseLayer_ChannelsSetMainProfileTab)]
    public async Task<TLBool> Handle(long authKeyId, TLBytes q)
    {
        var request = (ChannelsSetMainProfileTab)q;
        long? channelId = ResolveInputChannelId(request.Get_ChannelView());
        byte[]? tabBytes = ReadProfileTab(request.Get_TabView());
        if (tabBytes == null)
        {
            return ErrorBool("TAB_INVALID"u8);
        }

        var (currentUserId, channelBytes, error) = await PrepareChannelMutationCore(
            authKeyId, channelId, creatorOnly: false,
            ChatAdminRightRequirement.ChangeInfo);
        if (error != null)
        {
            return ErrorBool(Encoding.UTF8.GetBytes(error));
        }

        long id = channelId!.Value;
        int date = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
        using (TLChannelAdminState state = await LoadAdminStateAsync(id, date))
        using (TLChannelAdminState updated = ChannelAdminStateRows.WithMainTab(
                   state.AsChannelAdminState(), tabBytes, date))
        {
            _channelAdminRepository.PutState(updated);
        }

        await _unitOfWork.SaveAsync();
        await _fanout.PushUpdateChannelToOtherMembersAsync(id, currentUserId);
        _log.Debug($"📣 ChannelsSetMainProfileTab user:{currentUserId} channel:{id}");
        return new BoolTrue();
    }

    // The union is copied out of the request buffer rather than referenced,
    // because the row outlives the request. An unrecognised arm is refused
    // instead of stored, so `channelFull.main_tab` can never carry a
    // constructor the client cannot read.
    private static byte[]? ReadProfileTab(ProfileTabView view)
    {
        if (view.Is(out ProfileTabPosts posts)) return posts.ToReadOnlySpan().ToArray();
        if (view.Is(out ProfileTabGifts gifts)) return gifts.ToReadOnlySpan().ToArray();
        if (view.Is(out ProfileTabMedia media)) return media.ToReadOnlySpan().ToArray();
        if (view.Is(out ProfileTabFiles files)) return files.ToReadOnlySpan().ToArray();
        if (view.Is(out ProfileTabMusic music)) return music.ToReadOnlySpan().ToArray();
        if (view.Is(out ProfileTabVoice voice)) return voice.ToReadOnlySpan().ToArray();
        if (view.Is(out ProfileTabLinks links)) return links.ToReadOnlySpan().ToArray();
        if (view.Is(out ProfileTabGifs gifs)) return gifs.ToReadOnlySpan().ToArray();
        return null;
    }
}
