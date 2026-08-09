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

public sealed class UpdatePinnedMessageHandler : MessagesHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IMessageRepository _messageRepository;

    public UpdatePinnedMessageHandler(IUnitOfWork unitOfWork, IForumTopicsRepository forumTopicsRepository, IMessagingSettingsRepository messagingSettingsRepository, IAuthorizationRepository authorizationRepository, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,ISearchEngine search,
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

    [TLFunction(Constructors.baseLayer_UpdatePinnedMessage)]
    public async Task<TLUpdates> Handle(long authKeyId, TLBytes q)
        {
            var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
            if (auth == null)
            {
                return ErrorUpdates("AUTH_KEY_INVALID");
            }

            long userId = auth.Value.AsAuthInfo().UserId;
            var request = (UpdatePinnedMessage)q;
            bool pin = !request.Unpin;
            int messageId = request.Id;
            long pinChannelId = PeerResolver.ResolveInputPeerChannelId(request.Get_PeerView());
            if (pinChannelId > 0)
            {
                return await UpdatePinnedChannelMessage(authKeyId, userId, pinChannelId,
                    messageId, pin);
            }

            var (peerType, peerId) = PeerResolver.ResolveHistoryPeer(request.Get_PeerView(), userId);
            string? peerError = await ValidateCommonBoxPeer(userId, peerType, peerId,
                requireChatAdmin: true);
            if (peerError != null)
            {
                return ErrorUpdates(peerError);
            }

            var saved = await _messageRepository.GetMessageAsync(userId, messageId);
            if (saved == null)
            {
                return ErrorUpdates("MESSAGE_ID_INVALID");
            }

            int storedPts;
            using (var savedMessage = saved.Value)
            {
                var savedBody = savedMessage.AsSavedMessage();
                storedPts = savedBody.Pts;
                var original = savedBody.Get_OriginalMessage();
                if (original.Type != TLMessage.MessageType.Message ||
                    !MessageStore.TryReadStoredMessageInfo(original, out var info) ||
                    info.PeerType != peerType ||
                    info.PeerId != peerId)
                {
                    return ErrorUpdates("MESSAGE_ID_INVALID");
                }

                using TLMessage updated = original.AsMessage().Clone()
                    .Pinned(pin)
                    .Build();
                _messageRepository.PutMessage(userId, updated, storedPts);
            }

            if (peerType == TLPeer.PeerType.PeerChat)
            {
                await PutChatPinnedMessageId(peerId, pin ? messageId : 0,
                    pin ? null : messageId);
            }

            await _unitOfWork.SaveAsync();

            var userCtx = _updatesContextFactory.GetUpdatesContext(authKeyId, userId);
            int pts = await userCtx.IncrementPts();
            _log.Debug($"📌 UpdatePinnedMessage user:{userId} peerType:{peerType} " +
                       $"peer:{peerId} id:{messageId} pinned:{pin} pts:{pts}");
            return await _fanout.BuildPinnedMessagesResultAsync(userId, peerType, peerId,
                new[] { messageId }, pin, pts, 1);
        }
}
