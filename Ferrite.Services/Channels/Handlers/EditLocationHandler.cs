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
/// Sets or clears a supergroup's geographic location. The stored
/// `ChannelLocation` lives in the durable administration row and `has_geo` on
/// the compact channel row, because pinned TDLib reads the flag off the
/// compact row and the value out of `channelFull`.
///
/// `inputGeoPoint` and `geoPoint` do NOT share a field order --
/// `inputGeoPoint#48222faf` is `lat` then `long` and `geoPoint#b2a2f663` is
/// `long` then `lat` -- so the conversion goes through the generated builder by
/// name rather than by position.
/// </summary>
public sealed class EditLocationHandler : ChannelPropertyHandlerBase
{
    private readonly IChannelAdminRepository _channelAdminRepository;

    public EditLocationHandler(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChannelAdminRepository channelAdminRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,
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

    [TLFunction(Constructors.baseLayer_EditLocation)]
    public async Task<TLBool> Handle(long authKeyId, TLBytes q)
    {
        var request = (EditLocation)q;
        long? channelId = ResolveInputChannelId(request.Get_ChannelView());
        string address = Encoding.UTF8.GetString(request.Address);
        byte[]? geoPointBytes = BuildGeoPoint(request.Get_GeoPointView());

        var (currentUserId, channelBytes, error) = await PrepareChannelMutationCore(
            authKeyId, channelId, creatorOnly: false,
            ChatAdminRightRequirement.ChangeInfo);
        if (error != null)
        {
            return ErrorBool(Encoding.UTF8.GetBytes(error));
        }

        // Only a supergroup can be location-based; a broadcast has no locality.
        if (!ReadChannelFacts(channelBytes).Megagroup)
        {
            return ErrorBool("MEGAGROUP_REQUIRED"u8);
        }
        if (geoPointBytes != null && address.Length == 0)
        {
            return ErrorBool("ADDRESS_INVALID"u8);
        }

        long id = channelId!.Value;
        int date = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
        byte[] locationBytes = Array.Empty<byte>();
        if (geoPointBytes != null)
        {
            using TLChannelLocation location = ChannelLocation.Builder()
                .GeoPoint(geoPointBytes)
                .Address(Encoding.UTF8.GetBytes(address))
                .Build();
            locationBytes = location.AsSpan().ToArray();
        }

        byte[] previousLocation;
        using (TLChannelAdminState state = await LoadAdminStateAsync(id, date))
        {
            var view = state.AsChannelAdminState();
            previousLocation = OrEmptyLocation(view.Location);
            using TLChannelAdminState updated =
                ChannelAdminStateRows.WithLocation(view, locationBytes, date);
            _channelAdminRepository.PutState(updated);
        }

        StoreChannelFlags(channelBytes, flagBit: 21, flagValue: locationBytes.Length > 0);

        byte[] logAction;
        using (TLChannelAdminLogEventAction action =
               ChannelAdminLogEventActionChangeLocation.Builder()
                   .PrevValue(previousLocation)
                   .NewValue(OrEmptyLocation(locationBytes))
                   .Build())
        {
            logAction = action.AsSpan().ToArray();
        }
        await AppendAdminLogEventAsync(id, currentUserId, logAction, date, address);

        await _unitOfWork.SaveAsync();
        await _fanout.PushUpdateChannelToOtherMembersAsync(id, currentUserId);
        _log.Debug($"📣 EditLocation user:{currentUserId} channel:{id} " +
                   $"located:{locationBytes.Length > 0}");
        return new BoolTrue();
    }

    // `channelLocationEmpty` for a channel that has none: the admin-log action's
    // prev/new fields are not flag-gated, so absence needs a real value.
    private static byte[] OrEmptyLocation(ReadOnlySpan<byte> location)
    {
        if (location.Length > 0)
        {
            return location.ToArray();
        }

        using TLChannelLocation empty = ChannelLocationEmpty.Builder().Build();
        return empty.AsSpan().ToArray();
    }

    // The access hash of a geoPoint gates upload.getWebFile map previews, which
    // this deployment does not serve, so no token is issued for one.
    private static byte[]? BuildGeoPoint(InputGeoPointView view)
    {
        if (!view.Is(out InputGeoPoint point))
        {
            return null;
        }

        var builder = GeoPoint.Builder()
            .Lat(point.Lat)
            .Longitude(point.Longitude)
            .AccessHash(0);
        if (point.Flags[0])
        {
            builder = builder.AccuracyRadius(point.AccuracyRadius);
        }

        using TLGeoPoint geoPoint = builder.Build();
        return geoPoint.AsSpan().ToArray();
    }
}
