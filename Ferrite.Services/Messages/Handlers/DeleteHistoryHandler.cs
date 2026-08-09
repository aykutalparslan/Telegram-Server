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

public sealed class DeleteHistoryHandler : MessagesHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;

    public DeleteHistoryHandler(IUnitOfWork unitOfWork, IForumTopicsRepository forumTopicsRepository, IMessagingSettingsRepository messagingSettingsRepository, IAuthorizationRepository authorizationRepository, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,ISearchEngine search,
        IUpdatesService updates, IUpdatesContextFactory updatesContextFactory,
        ILogger log, IUploadService upload, IPhotoProcessingService photos,
        ICounterFactory counterFactory, IdAllocators ids,
        ChatRowStore chatRows, InviteStore invites,
        PrivacyEvaluator privacy, MessageStore messages, SendPipeline send,
        UpdateFanout fanout, DialogBuilder dialogs)
        : base(unitOfWork, forumTopicsRepository, messagingSettingsRepository, authorizationRepository, channelMessagesRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, search, updates, updatesContextFactory, log, upload, photos, counterFactory, ids, chatRows, invites, privacy, messages, send, fanout, dialogs)
    {
        _authorizationRepository = authorizationRepository;

    }

    [TLFunction(Constructors.baseLayer_MessagesDeleteHistory)]
    public async Task<TLAffectedHistory> Handle(long authKeyId, TLBytes q)
        {
            var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
            long userId = auth.Value.AsAuthInfo().UserId;
            var userCtx = _updatesContextFactory.GetUpdatesContext(authKeyId, userId);

            var request = (MessagesDeleteHistory)q;
            int maxId = request.MaxId;
            int? minDate = request.Flags[2] ? request.MinDate : null;
            int? maxDate = request.Flags[3] ? request.MaxDate : null;
            bool justClear = request.JustClear;
            bool revoke = request.Revoke;
            var (peerType, peerId) = PeerResolver.ResolveHistoryPeer(request.Get_PeerView(), userId);
            if (peerId <= 0)
            {
                int currentPts = await userCtx.Pts();
                _log.Debug($"🧹 DeleteHistory(unsupported) user:{userId} maxId:{maxId} pts:{currentPts}");
                return AffectedHistory.Builder().Pts(currentPts).PtsCount(0).Offset(0).Build();
            }

            // Caller side: clear the caller's own copy of the conversation up to maxId.
            var (pts, callerCount) = await DeleteConversation(userId, peerType,
                peerId, maxId, minDate, maxDate, userCtx);

            // Revoke (delete for everyone): also clear the partner's copy, but ONLY when the
            // request can be honored exactly. The partner stores its own message ids, so the
            // caller's maxId cannot be mapped onto them; a partial-by-id revoke would over-delete.
            // We therefore only clear the partner side for a full-history revoke (no maxId/date
            // bound), where "remove every message in this conversation" is unambiguous on both
            // sides. Partial or date/id-bounded revokes clear only the caller copy for now.
            // Group revokes need per-member id mapping and are deferred with the send fan-out.
            bool fullHistoryRevoke = maxId <= 0 && !minDate.HasValue && !maxDate.HasValue;
            if (revoke && !justClear && peerType == TLPeer.PeerType.PeerUser &&
                peerId != userId && fullHistoryRevoke)
            {
                var peerCtx = _updatesContextFactory.GetUpdatesContext(null, peerId);
                await DeleteConversation(peerId, TLPeer.PeerType.PeerUser, userId,
                    maxId: 0, minDate: null, maxDate: null, peerCtx);
            }

            _log.Debug($"🧹 DeleteHistory user:{userId} peerType:{peerType} peer:{peerId} maxId:{maxId} justClear:{justClear} revoke:{revoke} fullRevoke:{fullHistoryRevoke} count:{callerCount} pts:{pts}");
            return AffectedHistory.Builder().Pts(pts).PtsCount(callerCount).Offset(0).Build();
        }
}
