// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.channels;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

/// <summary>
/// Sets the channel's name colour or, with `for_profile`, its profile colour.
/// The two are separate fields on the compact `channel` row (`color:flags2.7` and
/// `profile_color:flags2.8`), so one request changes exactly one of them.
///
/// A request that names NEITHER `color` nor `background_emoji_id` CLEARS the
/// colour it addresses, which is how a client removes one. That clear is exactly
/// what a generated builder cannot express — it can set the gating flag but never
/// remove the value — so the row goes through the value-constructor rebuild.
/// </summary>
public sealed class ChannelsUpdateColorHandler : ChannelPropertyHandlerBase
{
    private readonly IChatRepository _chatRepository;

    public ChannelsUpdateColorHandler(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChannelAdminRepository channelAdminRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,
        ICounterFactory counterFactory, IdAllocators ids,
        IUpdatesContextFactory updatesContextFactory, IUpdatesService updates,
        ISearchEngine search, IUploadService upload,
        IPhotoProcessingService photos, ILogger log, ChatRowStore chatRows,
        UpdateFanout fanout)
        : base(unitOfWork, channelMessagesRepository, authorizationRepository, channelAdminLogRepository, channelAdminRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, counterFactory, ids, updatesContextFactory, updates,
            search, upload, photos, log, chatRows, fanout)
    {
        _chatRepository = chatRepository;

    }

    [TLFunction(Constructors.baseLayer_ChannelsUpdateColor)]
    public async Task<Ferrite.TL.baseLayer.TLUpdates> Handle(long authKeyId, TLBytes q)
    {
        var request = (ChannelsUpdateColor)q;
        long? channelId = ResolveInputChannelId(request.Get_ChannelView());
        bool forProfile = request.ForProfile;
        bool hasColor = request.Flags[2];
        bool hasEmoji = request.Flags[0];
        int color = request.Color;
        long backgroundEmojiId = request.BackgroundEmojiId;

        var (currentUserId, channelBytes, error) = await PrepareChannelMutation(
            authKeyId, channelId, creatorOnly: false,
            ChatAdminRightRequirement.ChangeInfo);
        if (error != null)
        {
            return error.Value;
        }

        long id = channelId!.Value;
        byte[] previousColor = ReadPeerColor(channelBytes, forProfile);
        byte[] updatedChannelBytes = StoreColor(channelBytes, forProfile, hasColor,
            color, hasEmoji, backgroundEmojiId);

        // Name and profile colour are distinct actions on the wire, matching the
        // two td_api entry points that share this method.
        byte[] logAction = BuildColorAction(forProfile, previousColor,
            ReadPeerColor(updatedChannelBytes, forProfile));
        await AppendAdminLogEventAsync(id, currentUserId, logAction,
            (int)DateTimeOffset.Now.ToUnixTimeSeconds());

        _log.Debug($"📣 UpdateColor user:{currentUserId} channel:{id} " +
                   $"profile:{forProfile} color:{(hasColor ? color : -1)} " +
                   $"emoji:{backgroundEmojiId}");
        return await CompleteAsync(authKeyId, currentUserId, id, updatedChannelBytes);
    }

    // The stored colour as its own bytes, or an empty `peerColor` when the channel
    // carries none. The admin-log action's prev/new fields are not flag-gated, so
    // "no colour" has to be a real value rather than an absent one.
    private static byte[] ReadPeerColor(byte[] channelBytes, bool forProfile)
    {
        using var stored = new TLChat(channelBytes, 0, channelBytes.Length);
        var channel = stored.AsChannel();
        Span<byte> value = forProfile ? channel.ProfileColor : channel.Color;
        if (value.Length > 0)
        {
            return value.ToArray();
        }

        using TLPeerColor empty = PeerColor.Builder().Build();
        return empty.AsSpan().ToArray();
    }

    private static byte[] BuildColorAction(bool forProfile, byte[] previous,
        byte[] current)
    {
        using TLChannelAdminLogEventAction action = forProfile
            ? ChannelAdminLogEventActionChangeProfilePeerColor.Builder()
                .PrevValue(previous).NewValue(current).Build()
            : ChannelAdminLogEventActionChangePeerColor.Builder()
                .PrevValue(previous).NewValue(current).Build();
        return action.AsSpan().ToArray();
    }

    // Synchronous by construction: the parsed row, the nested peerColor and the
    // rebuilt channel are all ref structs, and none of them may be alive across
    // the awaits in the caller.
    private byte[] StoreColor(byte[] channelBytes, bool forProfile, bool hasColor,
        int color, bool hasEmoji, long backgroundEmojiId)
    {
        using var stored = new TLChat(channelBytes, 0, channelBytes.Length);
        var channel = stored.AsChannel();

        if (!hasColor && !hasEmoji)
        {
            using TLChat cleared = ChannelRows.WithColor(channel, forProfile, default);
            _chatRepository.PutChat(cleared);
            return cleared.AsSpan().ToArray();
        }

        var colorBuilder = PeerColor.Builder();
        if (hasColor)
        {
            colorBuilder = colorBuilder.Color(color);
        }
        if (hasEmoji)
        {
            colorBuilder = colorBuilder.BackgroundEmojiId(backgroundEmojiId);
        }
        using TLPeerColor peerColor = colorBuilder.Build();
        using TLChat updated = ChannelRows.WithColor(channel, forProfile,
            peerColor.AsSpan());
        _chatRepository.PutChat(updated);
        return updated.AsSpan().ToArray();
    }
}
