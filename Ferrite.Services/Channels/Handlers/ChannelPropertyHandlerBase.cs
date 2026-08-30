// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.Services.Channels;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

public abstract class ChannelPropertyHandlerBase : ChannelsHandlerBase
{
    private readonly IChannelAdminRepository _channelAdminRepository;
    private readonly IChatRepository _chatRepository;

    protected ChannelPropertyHandlerBase(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChannelAdminRepository channelAdminRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,
        ICounterFactory counterFactory, IdAllocators ids,
        IUpdatesContextFactory updatesContextFactory, IUpdatesService updates,
        ISearchEngine search, IUploadService upload,
        IPhotoProcessingService photos, ILogger log, ChatRowStore chatRows,
        UpdateFanout fanout)
        : base(unitOfWork, channelMessagesRepository, authorizationRepository, channelAdminLogRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, counterFactory, ids, updatesContextFactory, updates,
            search, upload, photos, log, chatRows, fanout)
    {
        _channelAdminRepository = channelAdminRepository;
        _chatRepository = chatRepository;

    }

    protected readonly record struct ChannelFacts(Flags Flags, Flags Flags2,
        bool Megagroup, bool Broadcast, int ParticipantsCount);

    protected static ChannelFacts ReadChannelFacts(byte[] channelBytes)
    {
        using var stored = new TLChat(channelBytes, 0, channelBytes.Length);
        var channel = stored.AsChannel();
        return new ChannelFacts(channel.Flags, channel.Flags2, channel.Megagroup,
            channel.Broadcast, channel.ParticipantsCount);
    }

    protected byte[] StoreChannelFlags(byte[] channelBytes,
        int flagBit = -1, bool flagValue = false,
        int flags2Bit = -1, bool flags2Value = false)
    {
        using var stored = new TLChat(channelBytes, 0, channelBytes.Length);
        var channel = stored.AsChannel();
        Flags flags = channel.Flags;
        Flags flags2 = channel.Flags2;
        if (flagBit >= 0)
        {
            flags[flagBit] = flagValue;
        }
        if (flags2Bit >= 0)
        {
            flags2[flags2Bit] = flags2Value;
        }

        using TLChat updated = ChannelRows.WithFlags(channel, flags, flags2);
        _chatRepository.PutChat(updated);
        return updated.AsSpan().ToArray();
    }

    protected async Task<TLChannelAdminState> LoadAdminStateAsync(long channelId,
        int date)
    {
        TLChannelAdminState? stored = await _channelAdminRepository
            .GetStateAsync(channelId);
        return stored ?? ChannelAdminStateRows.Empty(channelId, date);
    }

    protected async Task<Ferrite.TL.baseLayer.TLUpdates> CompleteAsync(long authKeyId,
        long actorUserId, long channelId, byte[] channelBytes)
    {
        Ferrite.TL.baseLayer.TLUpdates result = await BuildChannelUpdates(authKeyId,
            actorUserId, channelBytes, Array.Empty<long>());
        await _fanout.PushUpdateChannelToOtherMembersAsync(channelId, actorUserId);
        return result;
    }
}
