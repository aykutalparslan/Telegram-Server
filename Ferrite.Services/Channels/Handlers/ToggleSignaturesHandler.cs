// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.Services.Channels;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.channels;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

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
