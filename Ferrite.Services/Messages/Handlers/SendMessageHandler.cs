// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.Services.Scheduling;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class SendMessageHandler : MessagesHandlerBase
{
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IForumTopicsRepository _forumTopicsRepository;
    private readonly IMessagingSettingsRepository _messagingSettingsRepository;

    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly ScheduledMessageSender _schedule;
    private readonly DraftStore _drafts;

    public SendMessageHandler(IUnitOfWork unitOfWork, IForumTopicsRepository forumTopicsRepository, IMessagingSettingsRepository messagingSettingsRepository, IAuthorizationRepository authorizationRepository, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,ISearchEngine search,
        IUpdatesService updates, IUpdatesContextFactory updatesContextFactory,
        ILogger log, IUploadService upload, IPhotoProcessingService photos,
        ICounterFactory counterFactory, IdAllocators ids,
        ChatRowStore chatRows, InviteStore invites,
        PrivacyEvaluator privacy, MessageStore messages, SendPipeline send,
        UpdateFanout fanout, DialogBuilder dialogs, ScheduledMessageSender schedule,
        DraftStore drafts)
        : base(unitOfWork, forumTopicsRepository, messagingSettingsRepository, authorizationRepository, channelMessagesRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, search, updates, updatesContextFactory, log, upload, photos, counterFactory, ids, chatRows, invites, privacy, messages, send, fanout, dialogs)
    {
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;
        _forumTopicsRepository = forumTopicsRepository;
        _messagingSettingsRepository = messagingSettingsRepository;

        _authorizationRepository = authorizationRepository;

        _schedule = schedule;
        _drafts = drafts;
    }

    [TLFunction(Constructors.baseLayer_SendMessage)]
    public async Task<TLUpdates> Handle(long authKeyId, TLBytes q)
        {
            var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
            if (auth == null)
            {
                return ErrorUpdates("AUTH_KEY_INVALID");
            }

            var userId = auth.Value.AsAuthInfo().UserId;
            var request = (SendMessage)q;
            int scheduleDate = request.Flags[10]
                ? request.ScheduleDate
                : 0;
            byte[] requestBytes = q.AsSpan().ToArray();
            using var to = PeerResolver.PeerFromInputPeer(request.Get_PeerView(), userId);
            TLPeer.PeerType peerType = to.Type;
            long peerId = GetPeerId(to);
            if (scheduleDate > 0 && _schedule.IsQueued(scheduleDate))
            {
                return await ScheduleTextMessage(authKeyId, q, userId, to.Type,
                    peerId, scheduleDate);
            }
            if (to.Type == TLPeer.PeerType.PeerChat)
            {
                return await SendBasicGroupMessage(authKeyId, q, userId, peerId,
                    () => ClearDraftAfterCommit(authKeyId, userId, peerType, peerId,
                        requestBytes));
            }
            if (to.Type == TLPeer.PeerType.PeerChannel)
            {
                return await SendChannelMessage(authKeyId, q, userId, peerId,
                    () => ClearDraftAfterCommit(authKeyId, userId, peerType, peerId,
                        requestBytes));
            }
            if (to.Type != TLPeer.PeerType.PeerUser || peerId <= 0)
            {
                return ErrorUpdates("PEER_ID_INVALID");
            }

            PreparedMessageTarget target = await MessageSendTargetResolver.PrepareAsync(_chatRepository, _chatParticipantsRepository, _forumTopicsRepository, _messagingSettingsRepository, userId, to.Type, peerId, requestBytes,
                new[] { ChatBannedAction.SendMessages });
            if (target.Error != null)
            {
                return ErrorUpdates(target.Error);
            }

            ShortSentBatch sent = await _send.SendPrivateMessageAsync(authKeyId, userId,
                to.Type, peerId, requestBytes);
            await ClearDraftAfterCommit(authKeyId, userId, peerType, peerId,
                requestBytes);

            return UpdateShortSentMessage.Builder()
                .OutProperty(true)
                .Id(sent.Id)
                .Pts(sent.Pts)
                .PtsCount(1)
                .Date(sent.Date)
                .Build();
        }

    private async Task<TLUpdates> ScheduleTextMessage(long authKeyId, TLBytes q,
        long userId, TLPeer.PeerType peerType, long peerId, int scheduleDate)
    {
        if (peerId <= 0)
        {
            return ErrorUpdates("PEER_ID_INVALID");
        }

        byte[] requestBytes = q.AsSpan().ToArray();
        PreparedMessageTarget target = await MessageSendTargetResolver.PrepareAsync(_chatRepository, _chatParticipantsRepository, _forumTopicsRepository, _messagingSettingsRepository, userId, peerType, peerId, requestBytes,
            new[] { ChatBannedAction.SendMessages });
        if (target.Error != null)
        {
            return ErrorUpdates(target.Error);
        }

        return await _schedule.ScheduleAsync(authKeyId, userId, target,
            new[] { new ScheduledMessageSender.ScheduledItem(requestBytes, null) },
            groupedId: 0, scheduleDate);
    }

    private async Task ClearDraftAfterCommit(long authKeyId, long userId,
        TLPeer.PeerType peerType, long peerId, byte[] requestBytes)
    {
        if (!await _drafts.ClearAfterSendAsync(authKeyId, userId, peerType, peerId,
                requestBytes))
        {
            _log.Warning($"Could not clear committed-send draft for " +
                         $"{peerType}:{peerId} user:{userId}");
        }
    }
}
