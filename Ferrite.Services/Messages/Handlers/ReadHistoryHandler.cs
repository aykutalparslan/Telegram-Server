// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class ReadHistoryHandler : MessagesHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly ReadReceiptStore _receipts;
    private readonly TimeProvider _timeProvider;

    public ReadHistoryHandler(IUnitOfWork unitOfWork, IForumTopicsRepository forumTopicsRepository, IMessagingSettingsRepository messagingSettingsRepository, IAuthorizationRepository authorizationRepository, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,ISearchEngine search,
        IUpdatesService updates, IUpdatesContextFactory updatesContextFactory,
        ILogger log, IUploadService upload, IPhotoProcessingService photos,
        ICounterFactory counterFactory, IdAllocators ids,
        ChatRowStore chatRows, InviteStore invites,
        PrivacyEvaluator privacy, MessageStore messages, SendPipeline send,
        UpdateFanout fanout, DialogBuilder dialogs, ReadReceiptStore receipts,
        TimeProvider timeProvider)
        : base(unitOfWork, forumTopicsRepository, messagingSettingsRepository, authorizationRepository, channelMessagesRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, search, updates, updatesContextFactory, log, upload, photos, counterFactory, ids, chatRows, invites, privacy, messages, send, fanout, dialogs)
    {
        _authorizationRepository = authorizationRepository;

        _receipts = receipts;
        _timeProvider = timeProvider;
    }

    [TLFunction(Constructors.baseLayer_MessagesReadHistory)]
    public async Task<TLAffectedMessages> Handle(long authKeyId, TLBytes q)
        {
            var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
            long userId = auth.Value.AsAuthInfo().UserId;
            var userCtx = _updatesContextFactory.GetUpdatesContext(authKeyId, userId);

            var request = (MessagesReadHistory)q;
            int maxId = request.MaxId;
            var (peerType, peerId) = PeerResolver.ResolveHistoryPeer(request.Get_PeerView(), userId);
            if (peerId <= 0)
            {
                int currentPts = await userCtx.Pts();
                _log.Debug($"👁 ReadHistory(unsupported) user:{userId} maxId:{maxId} pts:{currentPts}");
                return AffectedMessages.Builder().Pts(currentPts).PtsCount(0).Build();
            }

            // Dated receipts are written before the pointer moves, because the
            // window they cover is exactly what this read advances past. They are
            // what messages.getOutboxReadDate later reports to the sender.
            int previousMaxId = await userCtx.ReadMessagesMaxId((int)peerType, peerId);
            int receipts = await _receipts.RecordCommonReceiptsAsync(userId, peerType,
                peerId, previousMaxId, maxId,
                checked((int)_timeProvider.GetUtcNow().ToUnixTimeSeconds()));
            if (receipts > 0)
            {
                await _unitOfWork.SaveAsync();
            }

            // Mark the peer's inbound messages up to maxId as read; returns remaining unread.
            int stillUnread = await userCtx.ReadMessages((int)peerType, peerId, maxId);

            // Saved Messages (self): nothing to notify and no pts step, so the reported pts is
            // unchanged with pts_count 0.
            if (peerType == TLPeer.PeerType.PeerUser && peerId == userId)
            {
                int currentPts = await userCtx.Pts();
                _log.Debug($"👁 ReadHistory(self) user:{userId} maxId:{maxId} pts:{currentPts}");
                return AffectedMessages.Builder().Pts(currentPts).PtsCount(0).Build();
            }

            int userPts = await userCtx.IncrementPts();
            using TLPeer readPeer = PeerResolver.BuildPeer(peerType, peerId);
            TLUpdate inboxUpdate = UpdateReadHistoryInbox.Builder()
                .Peer(readPeer.AsSpan())
                .MaxId(maxId)
                .StillUnreadCount(stillUnread)
                .Pts(userPts)
                .PtsCount(1)
                .Build();
            await _updates.EnqueueUpdate(userId, inboxUpdate);

            // Outbox read notifications go to the single partner for private peers. Group
            // members' copies use per-member ids, so cross-member outbox read state is
            // deferred until per-member id mapping exists.
            if (peerType == TLPeer.PeerType.PeerUser)
            {
                var peerCtx = _updatesContextFactory.GetUpdatesContext(null, peerId);
                int peerPts = await peerCtx.IncrementPts();
                using TLPeer selfPeer = new PeerUser(userId);
                TLUpdate outboxUpdate = UpdateReadHistoryOutbox.Builder()
                    .Peer(selfPeer.AsSpan())
                    .MaxId(maxId)
                    .Pts(peerPts)
                    .PtsCount(1)
                    .Build();
                await _updates.EnqueueUpdate(peerId, outboxUpdate);
            }

            _log.Debug($"👁 ReadHistory user:{userId} peerType:{peerType} peer:{peerId} maxId:{maxId} stillUnread:{stillUnread} pts:{userPts}");
            return AffectedMessages.Builder().Pts(userPts).PtsCount(1).Build();
        }
}
