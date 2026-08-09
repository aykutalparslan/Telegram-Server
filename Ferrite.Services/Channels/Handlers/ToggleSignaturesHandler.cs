// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.Services.Channels;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.channels;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

/// <summary>
/// `channels.toggleSignatures` carries TWO independent flags and must not
/// collapse them: `signatures_enabled` shows the author's name under a post,
/// `profiles_enabled` additionally links it to their profile. They live in
/// different flag words of the compact `channel` row (`signatures` is `flags.11`,
/// `signature_profiles` is `flags2.12`), so both are written in one rebuild.
/// `toggle_channel_sign_messages` (`ChatManager.cpp:3310-3317`) refuses a
/// supergroup and needs change-info rights.
/// </summary>
public sealed class ToggleSignaturesHandler : ChannelPropertyHandlerBase
{
    public ToggleSignaturesHandler(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChannelAdminRepository channelAdminRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,
        ICounterFactory counterFactory, IdAllocators ids,
        IUpdatesContextFactory updatesContextFactory, IUpdatesService updates,
        ISearchEngine search, IUploadService upload,
        IPhotoProcessingService photos, ILogger log, ChatRowStore chatRows,
        UpdateFanout fanout)
        : base(unitOfWork, channelMessagesRepository, authorizationRepository, channelAdminLogRepository, channelAdminRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, counterFactory, ids, updatesContextFactory, updates,
            search, upload, photos, log, chatRows, fanout)
    {
    }

    [TLFunction(Constructors.baseLayer_ToggleSignatures)]
    public async Task<Ferrite.TL.baseLayer.TLUpdates> Handle(long authKeyId, TLBytes q)
    {
        var request = (ToggleSignatures)q;
        long? channelId = ResolveInputChannelId(request.Get_ChannelView());
        bool signatures = request.SignaturesEnabled;
        // Linking a signature to a profile is meaningless without the signature.
        bool profiles = signatures && request.ProfilesEnabled;

        var (currentUserId, channelBytes, error) = await PrepareChannelMutation(
            authKeyId, channelId, creatorOnly: false,
            ChatAdminRightRequirement.ChangeInfo);
        if (error != null)
        {
            return error.Value;
        }

        ChannelFacts facts = ReadChannelFacts(channelBytes);
        if (facts.Megagroup)
        {
            return ErrorUpdates("BROADCAST_REQUIRED"u8);
        }
        if (facts.Flags[11] == signatures && facts.Flags2[12] == profiles)
        {
            return ErrorUpdates("CHAT_NOT_MODIFIED"u8);
        }

        long id = channelId!.Value;
        byte[] updatedChannelBytes = StoreChannelFlags(channelBytes,
            flagBit: 11, flagValue: signatures,
            flags2Bit: 12, flags2Value: profiles);
        // The client sends both flags on every call (`ChatManager.cpp:571`), so
        // each is appended only when it actually moved; recording the unchanged
        // one would put an event in the log for a change nobody made.
        int date = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
        if (facts.Flags[11] != signatures)
        {
            await AppendAdminLogEventAsync(id, currentUserId,
                ChannelAdminLogRows.BoolAction(
                    ChannelAdminLogRows.BoolActionKind.Signatures, signatures), date);
        }
        if (facts.Flags2[12] != profiles)
        {
            await AppendAdminLogEventAsync(id, currentUserId,
                ChannelAdminLogRows.BoolAction(
                    ChannelAdminLogRows.BoolActionKind.SignatureProfiles, profiles), date);
        }

        _log.Debug($"📣 ToggleSignatures user:{currentUserId} channel:{id} " +
                   $"signatures:{signatures} profiles:{profiles}");
        return await CompleteAsync(authKeyId, currentUserId, id, updatedChannelBytes);
    }
}
