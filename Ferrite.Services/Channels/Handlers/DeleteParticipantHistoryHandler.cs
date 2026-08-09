// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using System.Text.RegularExpressions;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.channels;
using Ferrite.TL.baseLayer.dto;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

public sealed class DeleteParticipantHistoryHandler : ChannelsHandlerBase
{
    private readonly IChannelMessagesRepository _channelMessagesRepository;

    public DeleteParticipantHistoryHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository, ICounterFactory counterFactory,
        IdAllocators ids, IUpdatesContextFactory updatesContextFactory,
        IUpdatesService updates, ISearchEngine search, IUploadService upload,
        IPhotoProcessingService photos, ILogger log, ChatRowStore chatRows,
        UpdateFanout fanout)
        : base(unitOfWork, channelMessagesRepository, authorizationRepository, channelAdminLogRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, counterFactory, ids, updatesContextFactory, updates,
            search, upload, photos, log, chatRows, fanout)
    {
        _channelMessagesRepository = channelMessagesRepository;

    }

    [TLFunction(Constructors.baseLayer_DeleteParticipantHistory)]
    public async Task<Ferrite.TL.baseLayer.messages.TLAffectedHistory> Handle(
        long authKeyId, TLBytes q)
    {
        long? channelId = ResolveInputChannelId(((DeleteParticipantHistory)q).Get_ChannelView());

        var (currentUserId, _, error) = await PrepareChannelMutationCore(authKeyId,
            channelId, creatorOnly: false, ChatAdminRightRequirement.DeleteMessages);
        if (error != null)
        {
            return ErrorAffectedHistory(Encoding.UTF8.GetBytes(error));
        }

        long? participantId = ResolveInputPeerUserId(
            ((DeleteParticipantHistory)q).Get_ParticipantView(), currentUserId);
        if (participantId is not > 0)
        {
            return ErrorAffectedHistory("PARTICIPANT_ID_INVALID"u8);
        }

        long id = channelId!.Value;
        var channelBox = new ChannelMessageBox(_counterFactory, id);

        // Find every post authored by the participant across the whole channel box.
        var toDelete = new List<int>();
        var stored = await _channelMessagesRepository.GetMessagesAsync(id, 0, 0);
        foreach (var saved in stored)
        {
            using var s = saved;
            var original = s.AsSavedMessage().Get_OriginalMessage();
            if (ResolveMessageSenderId(original.AsSpan()) == participantId.Value)
            {
                toDelete.Add(MessageIds.GetId(original));
            }
        }

        foreach (int messageId in toDelete)
        {
            await _channelMessagesRepository.DeleteMessageAsync(id, messageId);
        }

        if (toDelete.Count == 0)
        {
            int currentPts = await channelBox.Pts();
            return Ferrite.TL.baseLayer.messages.AffectedHistory.Builder()
                .Pts(currentPts).PtsCount(0).Offset(0).Build();
        }

        int pts = await channelBox.IncrementPts(toDelete.Count);
        await _fanout.PushDeleteChannelMessagesAsync(id, currentUserId, toDelete, pts,
            toDelete.Count);
        await _unitOfWork.SaveAsync();

        _log.Debug($"📣 channels.DeleteParticipantHistory user:{currentUserId} channel:{id} " +
                   $"participant:{participantId.Value} deleted:{toDelete.Count} pts:{pts}");
        return Ferrite.TL.baseLayer.messages.AffectedHistory.Builder()
            .Pts(pts).PtsCount(toDelete.Count).Offset(0).Build();
    }
}
