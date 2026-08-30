// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using System.Text.RegularExpressions;
using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.channels;
using Ferrite.TL.baseLayer.dto;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

public sealed class ReadHistoryHandler : ChannelsHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChannelMessagesRepository _channelMessagesRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;

    private readonly ReadReceiptStore _receipts;
    private readonly TimeProvider _timeProvider;

    public ReadHistoryHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository, ICounterFactory counterFactory,
        IdAllocators ids, IUpdatesContextFactory updatesContextFactory,
        IUpdatesService updates, ISearchEngine search, IUploadService upload,
        IPhotoProcessingService photos, ILogger log, ChatRowStore chatRows,
        UpdateFanout fanout, ReadReceiptStore receipts, TimeProvider timeProvider)
        : base(unitOfWork, channelMessagesRepository, authorizationRepository, channelAdminLogRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, counterFactory, ids, updatesContextFactory, updates,
            search, upload, photos, log, chatRows, fanout)
    {
        _authorizationRepository = authorizationRepository;
        _channelMessagesRepository = channelMessagesRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;

        _receipts = receipts;
        _timeProvider = timeProvider;
    }

    [TLFunction(Constructors.baseLayer_ChannelsReadHistory)]
    public async Task<TLBool> Handle(long authKeyId, TLBytes q)
    {
        var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
        if (auth == null)
        {
            return (TLBool)RpcErrorGenerator.GenerateError(400, "AUTH_KEY_INVALID"u8);
        }

        long currentUserId = auth.Value.AsAuthInfo().UserId;
        var request = (ChannelsReadHistory)q;
        long? channelId = ResolveInputChannelId(request.Get_ChannelView());
        int maxId = request.MaxId;
        if (channelId is not > 0)
        {
            return (TLBool)RpcErrorGenerator.GenerateError(400, "CHANNEL_INVALID"u8);
        }

        using var channel = await _chatRepository.GetChatAsync(channelId.Value);
        if (channel == null || channel.Value.Type != TLChat.ChatType.Channel)
        {
            return (TLBool)RpcErrorGenerator.GenerateError(400, "CHANNEL_INVALID"u8);
        }

        var participant = await _chatParticipantsRepository
            .GetParticipantAsync(channelId.Value, currentUserId);
        bool member = participant != null && IsActiveParticipant(participant.Value);
        participant?.Dispose();
        if (!member)
        {
            return (TLBool)RpcErrorGenerator.GenerateError(400, "CHANNEL_PRIVATE"u8);
        }

        int existingInbox = 0;
        int existingOutbox = 0;
        using (var readState = await _channelMessagesRepository
                   .GetReadStateAsync(currentUserId, channelId.Value))
        {
            if (readState != null)
            {
                var state = readState.Value.AsChannelReadState();
                existingInbox = state.ReadInboxMaxId;
                existingOutbox = state.ReadOutboxMaxId;
            }
        }
        int newInbox = Math.Max(existingInbox, maxId);

        using (TLChannelReadState updated = ChannelReadState.Builder()
                   .UserId(currentUserId)
                   .ChannelId(channelId.Value)
                   .ReadInboxMaxId(newInbox)
                   .ReadOutboxMaxId(existingOutbox)
                   .Build())
        {
            _channelMessagesRepository.PutReadState(updated);
        }

        await _receipts.RecordChannelReceiptsAsync(currentUserId, channelId.Value,
            existingInbox, maxId,
            checked((int)_timeProvider.GetUtcNow().ToUnixTimeSeconds()));

        int stillUnread = 0;
        var unread = await _channelMessagesRepository
            .GetMessagesAsync(channelId.Value, newInbox + 1, 0);
        foreach (var saved in unread)
        {
            using var s = saved;
            long sender = ResolveMessageSenderId(s.AsSavedMessage().Get_OriginalMessage().AsSpan());
            if (sender != currentUserId)
            {
                stillUnread++;
            }
        }

        var channelBox = new ChannelMessageBox(_counterFactory, channelId.Value);
        int channelPts = await channelBox.Pts();

        await _unitOfWork.SaveAsync();

        TLUpdate inboxUpdate = UpdateReadChannelInbox.Builder()
            .ChannelId(channelId.Value)
            .MaxId(newInbox)
            .StillUnreadCount(stillUnread)
            .Pts(channelPts)
            .Build();
        await _updates.EnqueueUpdate(currentUserId, inboxUpdate);

        _log.Debug($"📣 channels.ReadHistory user:{currentUserId} channel:{channelId.Value} " +
                   $"maxId:{newInbox} unread:{stillUnread} pts:{channelPts}");
        return BoolTrue.Builder().Build();
    }
}
