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

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class GetPeerSettingsHandler : MessagesHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IUserRepository _userRepository;

    private readonly ModerationStore _moderation;

    public GetPeerSettingsHandler(IUnitOfWork unitOfWork, IForumTopicsRepository forumTopicsRepository, IMessagingSettingsRepository messagingSettingsRepository, IAuthorizationRepository authorizationRepository, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,ISearchEngine search,
        IUpdatesService updates, IUpdatesContextFactory updatesContextFactory,
        ILogger log, IUploadService upload, IPhotoProcessingService photos,
        ICounterFactory counterFactory, IdAllocators ids,
        ChatRowStore chatRows, InviteStore invites,
        PrivacyEvaluator privacy, MessageStore messages, SendPipeline send,
        UpdateFanout fanout, DialogBuilder dialogs, ModerationStore moderation)
        : base(unitOfWork, forumTopicsRepository, messagingSettingsRepository, authorizationRepository, channelMessagesRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, search, updates, updatesContextFactory, log, upload, photos, counterFactory, ids, chatRows, invites, privacy, messages, send, fanout, dialogs)
    {
        _authorizationRepository = authorizationRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;
        _userRepository = userRepository;

        _moderation = moderation;
    }

    [TLFunction(Constructors.baseLayer_GetPeerSettings)]
    public async Task<Ferrite.TL.baseLayer.messages.TLPeerSettings> Handle(long authKeyId, TLBytes q)
        {
            var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
            if (auth == null)
            {
                return (Ferrite.TL.baseLayer.messages.TLPeerSettings)RpcErrorGenerator
                    .GenerateError(400, "AUTH_KEY_INVALID"u8);
            }

            long currentUserId = auth.Value.AsAuthInfo().UserId;
            using TLPeer peer = PeerResolver.PeerFromInputPeer(((GetPeerSettings)q).Get_PeerView(), currentUserId);
            long peerId = GetPeerId(peer);
            byte[]? chatBytes = null;
            bool suggestReportSpam = false;
            if (peer.Type == TLPeer.PeerType.PeerUser)
            {
                using var user = _userRepository.GetUser(peerId);
                if (user == null)
                {
                    return (Ferrite.TL.baseLayer.messages.TLPeerSettings)RpcErrorGenerator
                        .GenerateError(400, "PEER_ID_INVALID"u8);
                }

                suggestReportSpam = await _moderation
                    .ShouldOfferPrivateActionBarAsync(currentUserId, peerId);
            }
            else if (peer.Type == TLPeer.PeerType.PeerChat)
            {
                var participant = await _chatParticipantsRepository
                    .GetParticipantAsync(peerId, currentUserId);
                if (participant == null || !IsActiveParticipant(participant.Value))
                {
                    participant?.Dispose();
                    return (Ferrite.TL.baseLayer.messages.TLPeerSettings)RpcErrorGenerator
                        .GenerateError(400, "USER_NOT_PARTICIPANT"u8);
                }
                participant.Value.Dispose();

                using var chat = await _chatRepository.GetChatAsync(peerId);
                if (chat == null)
                {
                    return (Ferrite.TL.baseLayer.messages.TLPeerSettings)RpcErrorGenerator
                        .GenerateError(400, "CHAT_ID_INVALID"u8);
                }
                chatBytes = chat.Value.AsSpan().ToArray();
            }
            else
            {
                return (Ferrite.TL.baseLayer.messages.TLPeerSettings)RpcErrorGenerator
                    .GenerateError(400, "PEER_ID_INVALID"u8);
            }

            var settingsBuilder = PeerSettings.Builder();
            if (suggestReportSpam)
            {
                settingsBuilder = settingsBuilder
                    .ReportSpam(true)
                    .AddContact(true)
                    .BlockContact(true);
            }
            using Ferrite.TL.baseLayer.TLPeerSettings settings = settingsBuilder.Build();
            var chatVector = new Vector();
            if (chatBytes != null)
            {
                chatVector.AppendTLObject(chatBytes);
            }

            var userVector = new Vector();
            if (peer.Type == TLPeer.PeerType.PeerUser)
            {
                AppendUsers(currentUserId, ref userVector, new[] { peerId });
            }

            return MessagesPeerSettings.Builder()
                .Settings(settings.AsSpan())
                .Chats(chatVector)
                .Users(userVector)
                .Build();
        }
}
