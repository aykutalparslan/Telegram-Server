// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;
using Ferrite.Utils;
using Layer120UnpinAllMessages = Ferrite.TL.layer120.messages.MessagesUnpinAllMessages;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class UnpinAllMessagesHandler : MessagesHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IMessageRepository _messageRepository;

    public UnpinAllMessagesHandler(IUnitOfWork unitOfWork, IForumTopicsRepository forumTopicsRepository, IMessagingSettingsRepository messagingSettingsRepository, IAuthorizationRepository authorizationRepository, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,ISearchEngine search,
        IUpdatesService updates, IUpdatesContextFactory updatesContextFactory,
        ILogger log, IUploadService upload, IPhotoProcessingService photos,
        ICounterFactory counterFactory, IdAllocators ids,
        ChatRowStore chatRows, InviteStore invites,
        PrivacyEvaluator privacy, MessageStore messages, SendPipeline send,
        UpdateFanout fanout, DialogBuilder dialogs)
        : base(unitOfWork, forumTopicsRepository, messagingSettingsRepository, authorizationRepository, channelMessagesRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, search, updates, updatesContextFactory, log, upload, photos, counterFactory, ids, chatRows, invites, privacy, messages, send, fanout, dialogs)
    {
        _authorizationRepository = authorizationRepository;
        _messageRepository = messageRepository;

    }

    [TLFunction(Constructors.layer120_MessagesUnpinAllMessages)]
    public async Task<TLAffectedHistory> HandleLayer120(long authKeyId, TLBytes q)
    {
        using var current = ToCurrentUnpinAllMessagesRequest(q);
        return await Handle(authKeyId, current);
    }

    private static TLBytes ToCurrentUnpinAllMessagesRequest(TLBytes q)
    {
        var sent = new Layer120UnpinAllMessages(q.AsSpan());
        using var current = UnpinAllMessages.Builder()
            .Peer(sent.Peer)
            .Build();
        return current.TLBytes!.Value;
    }

    [TLFunction(Constructors.baseLayer_UnpinAllMessages)]
    public async Task<TLAffectedHistory> Handle(long authKeyId, TLBytes q)
        {
            var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
            if (auth == null)
            {
                return ErrorAffectedHistory("AUTH_KEY_INVALID");
            }

            long userId = auth.Value.AsAuthInfo().UserId;
            var request = (UnpinAllMessages)q;
            if (request.Flags[0] || request.Flags[1])
            {
                return ErrorAffectedHistory("PEER_ID_INVALID");
            }

            long unpinChannelId = PeerResolver.ResolveInputPeerChannelId(request.Get_PeerView());
            if (unpinChannelId > 0)
            {
                return await UnpinAllChannelMessages(userId, unpinChannelId);
            }

            var (peerType, peerId) = PeerResolver.ResolveHistoryPeer(request.Get_PeerView(), userId);
            string? peerError = await ValidateCommonBoxPeer(userId, peerType, peerId,
                requireChatAdmin: true);
            if (peerError != null)
            {
                return ErrorAffectedHistory(peerError);
            }

            var saved = await _messageRepository.GetMessagesAsync(userId);
            var unpinnedIds = new List<int>();
            foreach (var s in saved)
            {
                using var savedMessage = s;
                var savedBody = savedMessage.AsSavedMessage();
                var original = savedBody.Get_OriginalMessage();
                if (original.Type != TLMessage.MessageType.Message ||
                    !MessageStore.TryReadStoredMessageInfo(original, out var info) ||
                    info.PeerType != peerType ||
                    info.PeerId != peerId)
                {
                    continue;
                }

                var message = original.AsMessage();
                if (!message.Pinned)
                {
                    continue;
                }

                using TLMessage updated = message.Clone()
                    .Pinned(false)
                    .Build();
                _messageRepository.PutMessage(userId, updated, savedBody.Pts);
                unpinnedIds.Add(message.Id);
            }

            if (peerType == TLPeer.PeerType.PeerChat && unpinnedIds.Count > 0)
            {
                await PutChatPinnedMessageId(peerId, 0);
            }

            await _unitOfWork.SaveAsync();

            var userCtx = _updatesContextFactory.GetUpdatesContext(authKeyId, userId);
            if (unpinnedIds.Count == 0)
            {
                int currentPts = await userCtx.Pts();
                return AffectedHistory.Builder()
                    .Pts(currentPts)
                    .PtsCount(0)
                    .Offset(0)
                    .Build();
            }

            int pts = await userCtx.IncrementPts(unpinnedIds.Count);
            TLUpdate update = UpdateFanout.BuildPinnedMessagesUpdate(peerType, peerId,
                unpinnedIds,
                pinned: false, pts, unpinnedIds.Count);
            await _updates.EnqueueUpdate(userId, update);
            _log.Debug($"📌 UnpinAllMessages user:{userId} peerType:{peerType} " +
                       $"peer:{peerId} count:{unpinnedIds.Count} pts:{pts}");
            return AffectedHistory.Builder()
                .Pts(pts)
                .PtsCount(unpinnedIds.Count)
                .Offset(0)
                .Build();
        }
}
