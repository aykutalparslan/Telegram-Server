// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.Services.Channels;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

/// <summary>
/// Shared plumbing for property toggles: the ones that mutate one
/// channel property and answer <c>Updates</c>.
///
/// Every one of them MUST finish through <see cref="CompleteAsync"/>, which fans
/// the change out to every OTHER member. Placing <c>updateChannel</c> only in the
/// actor's own RPC result is the `7630f49c` defect: the actor sees the change,
/// every other member keeps a cached <c>channelFull</c> forever, and the
/// diagnostic signature is ZERO requests from that member, so there is no failing
/// request to find.
/// </summary>
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

    /// <summary>
    /// The facts a toggle needs before it decides anything. Read in one
    /// synchronous frame so no view outlives its buffer across an await.
    /// </summary>
    protected readonly record struct ChannelFacts(Flags Flags, Flags Flags2,
        bool Megagroup, bool Broadcast, int ParticipantsCount);

    protected static ChannelFacts ReadChannelFacts(byte[] channelBytes)
    {
        using var stored = new TLChat(channelBytes, 0, channelBytes.Length);
        var channel = stored.AsChannel();
        return new ChannelFacts(channel.Flags, channel.Flags2, channel.Megagroup,
            channel.Broadcast, channel.ParticipantsCount);
    }

    /// <summary>
    /// Rewrites and persists the stored channel row with up to one bare flag
    /// changed in each flag word. A bit index below zero leaves that word alone.
    /// Two independent bits are expressible because <c>toggleSignatures</c> owns
    /// <c>signatures</c> and <c>signature_profiles</c> separately and must not
    /// collapse them into one.
    /// </summary>
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

    /// <summary>
    /// The stored administration row, or the all-clear row a channel without one
    /// behaves as, so callers mutate rather than branch on absence. The result is
    /// owned and must be disposed.
    /// </summary>
    protected async Task<TLChannelAdminState> LoadAdminStateAsync(long channelId,
        int date)
    {
        TLChannelAdminState? stored = await _channelAdminRepository
            .GetStateAsync(channelId);
        return stored ?? ChannelAdminStateRows.Empty(channelId, date);
    }

    /// <summary>
    /// Persists everything the toggle wrote, answers the actor with
    /// <c>Updates(updateChannel)</c>, and pushes the same update to every other
    /// active member. Order matters: the fanout runs after the save so nobody
    /// re-reads a stale row.
    /// </summary>
    protected async Task<Ferrite.TL.baseLayer.TLUpdates> CompleteAsync(long authKeyId,
        long actorUserId, long channelId, byte[] channelBytes)
    {
        Ferrite.TL.baseLayer.TLUpdates result = await BuildChannelUpdates(authKeyId,
            actorUserId, channelBytes, Array.Empty<long>());
        await _fanout.PushUpdateChannelToOtherMembersAsync(channelId, actorUserId);
        return result;
    }
}
