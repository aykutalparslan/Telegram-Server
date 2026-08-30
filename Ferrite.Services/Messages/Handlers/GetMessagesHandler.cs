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

public sealed class GetMessagesHandler : MessagesHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IMessageRepository _messageRepository;

    public GetMessagesHandler(IUnitOfWork unitOfWork, IForumTopicsRepository forumTopicsRepository, IMessagingSettingsRepository messagingSettingsRepository, IAuthorizationRepository authorizationRepository, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,ISearchEngine search,
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

    [TLFunction(Constructors.layer2_MessagesGetMessages)]
    public async Task<TLMessages> HandleLayer2(long authKeyId, TLBytes q)
    {
        using var current = ToCurrentGetMessagesRequest(q);
        return await Handle(authKeyId, current);
    }

    private static TLBytes ToCurrentGetMessagesRequest(TLBytes q)
    {
        var sent = new TL.layer2.messages.MessagesGetMessages(q.AsSpan());
        var ids = new Vector();
        VectorOfInt sentIds = sent.Id;
        for (int i = 0; i < sentIds.Count; i++)
        {
            using var id = InputMessageID.Builder().Id(sentIds[i]).Build();
            ids.AppendTLObject(id.ToReadOnlySpan());
        }
        using var current = MessagesGetMessages.Builder()
            .Id(ids)
            .Build();
        return current.TLBytes!.Value;
    }

    [TLFunction(Constructors.baseLayer_MessagesGetMessages)]
    public async Task<TLMessages> Handle(long authKeyId, TLBytes q)
        {
            var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
            long userId = auth.Value.AsAuthInfo().UserId;

            var messageBytes = new List<byte[]>();
            var relatedUserIds = new HashSet<long>();
            var relatedChatIds = new HashSet<long>();

            var idVector = ((MessagesGetMessages)q).Id;
            int count = idVector.Count;
            for (int i = 0; i < count; i++)
            {
                InputMessageView inputMessage = idVector.ReadTLObject();
                if (!inputMessage.Is(out InputMessageID messageById))
                {
                    continue;
                }

                int messageId = messageById.Id;
                var saved = _messageRepository.GetMessage(userId, messageId);
                if (saved == null)
                {
                    continue;
                }

                using var savedMessage = saved.Value;
                var message = savedMessage.AsSavedMessage().Get_OriginalMessage();
                messageBytes.Add(message.AsSpan().ToArray());
                AddMessageRelatedPeers(message, relatedUserIds, relatedChatIds);
            }

            var relatedChatBytes = await GetChatBytesForViewer(userId, relatedChatIds);
            var messageVector = new Vector();
            foreach (byte[] message in messageBytes)
            {
                messageVector.AppendTLObject(message);
            }
            var userVector = new Vector();
            AppendUsers(userId, ref userVector, relatedUserIds);
            var chatVector = new Vector();
            foreach (byte[] chat in relatedChatBytes)
            {
                chatVector.AppendTLObject(chat);
            }

            return Ferrite.TL.baseLayer.messages.Messages.Builder()
                .MessagesProperty(messageVector)
                .Chats(chatVector)
                .Users(userVector)
                .Build();
        }
}
