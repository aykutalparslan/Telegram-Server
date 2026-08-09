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

public sealed class DeleteMessagesHandler : ChannelsHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChannelMessagesRepository _channelMessagesRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;

    public DeleteMessagesHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository, ICounterFactory counterFactory,
        IdAllocators ids, IUpdatesContextFactory updatesContextFactory,
        IUpdatesService updates, ISearchEngine search, IUploadService upload,
        IPhotoProcessingService photos, ILogger log, ChatRowStore chatRows,
        UpdateFanout fanout)
        : base(unitOfWork, channelMessagesRepository, authorizationRepository, channelAdminLogRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, counterFactory, ids, updatesContextFactory, updates,
            search, upload, photos, log, chatRows, fanout)
    {
        _authorizationRepository = authorizationRepository;
        _channelMessagesRepository = channelMessagesRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;

    }

    [TLFunction(Constructors.baseLayer_ChannelsDeleteMessages)]
    public async Task<Ferrite.TL.baseLayer.messages.TLAffectedMessages> Handle(
        long authKeyId, TLBytes q)
    {
        // Resolve the channel + requested ids off the ref-struct request before any await.
        var requestedIds = new List<int>();
        var request = (ChannelsDeleteMessages)q;
        long? channelId = ResolveInputChannelId(request.Get_ChannelView());
        var idVector = request.Id;
        int idCount = idVector.Count;
        for (int i = 0; i < idCount; i++)
        {
            int requestedId = idVector[i];
            if (requestedId > 0 && !requestedIds.Contains(requestedId))
            {
                requestedIds.Add(requestedId);
            }
        }

        var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
        if (auth == null)
        {
            return ErrorAffectedMessages("AUTH_KEY_INVALID"u8);
        }

        long currentUserId = auth.Value.AsAuthInfo().UserId;
        if (channelId is not > 0)
        {
            return ErrorAffectedMessages("CHANNEL_INVALID"u8);
        }

        long id = channelId.Value;
        using var channel = await _chatRepository.GetChatAsync(id);
        if (channel == null || channel.Value.Type != TLChat.ChatType.Channel)
        {
            return ErrorAffectedMessages("CHANNEL_INVALID"u8);
        }

        // Creator/admins with the delete_messages right delete any post; every other
        // active member may still delete their OWN posts. Posts the caller cannot
        // delete are skipped rather than failing the request.
        var participant = await _chatParticipantsRepository
            .GetParticipantAsync(id, currentUserId);
        if (participant == null || !IsActiveParticipant(participant.Value))
        {
            participant?.Dispose();
            return ErrorAffectedMessages("USER_NOT_PARTICIPANT"u8);
        }
        bool canDeleteOthers = ChatRights.HasAdminRight(participant.Value,
            ChatAdminRightRequirement.DeleteMessages);
        participant.Value.Dispose();

        var channelBox = new ChannelMessageBox(_counterFactory, id);

        // Only count posts that actually existed so the channel pts_count stays exact.
        var deletedIds = new List<int>();
        var loggedMessages = new List<byte[]>();
        foreach (int messageId in requestedIds)
        {
            var saved = await _channelMessagesRepository.GetMessageAsync(id, messageId);
            if (saved == null)
            {
                continue;
            }
            bool deletable;
            byte[] messageBytes;
            using (var savedMessage = saved.Value)
            {
                messageBytes = savedMessage.AsSavedMessage().Get_OriginalMessage()
                    .AsSpan().ToArray();
                deletable = canDeleteOthers ||
                    ResolveMessageSenderId(messageBytes) == currentUserId;
            }
            if (!deletable)
            {
                continue;
            }
            await _channelMessagesRepository.DeleteMessageAsync(id, messageId);
            deletedIds.Add(messageId);
            // Only a deletion performed with the delete-messages right is an
            // administrative act. A member removing their own post is not, and
            // recording it would put every ordinary self-delete in the admin log.
            if (canDeleteOthers)
            {
                loggedMessages.Add(messageBytes);
            }
        }

        if (deletedIds.Count == 0)
        {
            int currentPts = await channelBox.Pts();
            return Ferrite.TL.baseLayer.messages.AffectedMessages.Builder()
                .Pts(currentPts).PtsCount(0).Build();
        }

        int deleteDate = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
        foreach (byte[] messageBytes in loggedMessages)
        {
            byte[] logAction;
            using (TLChannelAdminLogEventAction action =
                   ChannelAdminLogEventActionDeleteMessage.Builder()
                       .Message(messageBytes)
                       .Build())
            {
                logAction = action.AsSpan().ToArray();
            }
            await AppendAdminLogEventAsync(id, currentUserId, logAction, deleteDate,
                ReadMessageSearchText(messageBytes));
        }

        int pts = await channelBox.IncrementPts(deletedIds.Count);
        await _fanout.PushDeleteChannelMessagesAsync(id, currentUserId, deletedIds,
            pts, deletedIds.Count);
        await _unitOfWork.SaveAsync();

        _log.Debug($"📣 channels.DeleteMessages user:{currentUserId} channel:{id} " +
                   $"deleted:{deletedIds.Count} pts:{pts}");
        return Ferrite.TL.baseLayer.messages.AffectedMessages.Builder()
            .Pts(pts).PtsCount(deletedIds.Count).Build();
    }

    // What `q` matches a deleted post on: its own text. A service message carries
    // none, so it contributes nothing rather than a placeholder.
    private static string ReadMessageSearchText(byte[] messageBytes)
    {
        var message = (Ferrite.TL.baseLayer.Message)messageBytes.AsSpan();
        return message.Constructor == Constructors.baseLayer_Message &&
               message.MessageProperty.Length > 0
            ? Encoding.UTF8.GetString(message.MessageProperty)
            : string.Empty;
    }
}
