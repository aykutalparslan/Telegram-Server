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

public sealed class CreateChatHandler : MessagesHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatInvitesRepository _chatInvitesRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IMessageRepository _messageRepository;

    private readonly ChatSettingsStore _settings;

    public CreateChatHandler(IUnitOfWork unitOfWork, IForumTopicsRepository forumTopicsRepository, IMessagingSettingsRepository messagingSettingsRepository, IAuthorizationRepository authorizationRepository, IChannelMessagesRepository channelMessagesRepository, IChatInvitesRepository chatInvitesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,ISearchEngine search,
        IUpdatesService updates, IUpdatesContextFactory updatesContextFactory,
        ILogger log, IUploadService upload, IPhotoProcessingService photos,
        ICounterFactory counterFactory, IdAllocators ids,
        ChatRowStore chatRows, InviteStore invites,
        PrivacyEvaluator privacy, MessageStore messages, SendPipeline send,
        UpdateFanout fanout, DialogBuilder dialogs, ChatSettingsStore settings)
        : base(unitOfWork, forumTopicsRepository, messagingSettingsRepository, authorizationRepository, channelMessagesRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, search, updates, updatesContextFactory, log, upload, photos, counterFactory, ids, chatRows, invites, privacy, messages, send, fanout, dialogs)
    {
        _authorizationRepository = authorizationRepository;
        _chatInvitesRepository = chatInvitesRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;
        _messageRepository = messageRepository;

        _settings = settings;
    }

    [TLFunction(Constructors.layer150_MessagesCreateChat)]
    public async Task<TLInvitedUsers> HandleLayer150(long authKeyId, TLBytes q)
    {
        using var current = ToCurrentCreateChatRequest(q);
        return await Handle(authKeyId, current);
    }

    private static TLBytes ToCurrentCreateChatRequest(TLBytes q)
    {
        var sent = new TL.layer150.messages.MessagesCreateChat(q.AsSpan());
        var builder = CreateChat.Builder()
            .Users(sent.Users)
            .Title(sent.Title);
        if (sent.Flags[0])
        {
            builder = builder.TtlPeriod(sent.TtlPeriod);
        }
        var current = builder.Build();
        return current.TLBytes!.Value;
    }

    [TLFunction(Constructors.baseLayer_CreateChat)]
    public async Task<TLInvitedUsers> Handle(long authKeyId, TLBytes q)
        {
            var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
            if (auth == null)
            {
                return (TLInvitedUsers)RpcErrorGenerator.GenerateError(400, "AUTH_KEY_INVALID"u8);
            }

            long creatorUserId = auth.Value.AsAuthInfo().UserId;
            var request = (CreateChat)q;
            byte[] title = request.Title.ToArray();
            int requestedTtlPeriod = request.Flags[0] ? request.TtlPeriod : 0;
            List<long> participantIds = ResolveInputUserIds(request.Users, creatorUserId);
            if (!participantIds.Contains(creatorUserId))
            {
                participantIds.Insert(0, creatorUserId);
            }

            participantIds = participantIds.Distinct().ToList();
            if (participantIds.Count < 2)
            {
                return (TLInvitedUsers)RpcErrorGenerator.GenerateError(400, "USERS_TOO_FEW"u8);
            }
            if (!AllUsersExist(participantIds))
            {
                return (TLInvitedUsers)RpcErrorGenerator.GenerateError(400, "USER_ID_INVALID"u8);
            }

            var missingInviteeIds = new List<long>();
            var allowedParticipantIds = new List<long>(participantIds.Count);
            foreach (long participantId in participantIds)
            {
                if (participantId == creatorUserId ||
                    await _privacy.IsChatInviteAllowed(creatorUserId, participantId))
                {
                    allowedParticipantIds.Add(participantId);
                }
                else
                {
                    missingInviteeIds.Add(participantId);
                }
            }

            participantIds = allowedParticipantIds;

            long chatId = await _ids.NextChatIdAsync();
            int date = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
            if (requestedTtlPeriod > 0)
            {
                _settings.Put(ChatSettingsScope.ForChat(chatId),
                    ChatSettingsSnapshot.Empty with { TtlPeriod = requestedTtlPeriod });
            }

            byte[] chatBytes;
            {
                using var chatPhoto = ChatPhotoEmpty.Builder().Build();
                byte[] defaultBannedRights =
                    ChatRights.BuildUnrestrictedDefaultBannedRights();
                using TLChat chatToStore = Chat.Builder()
                    .Creator(true)
                    .Id(chatId)
                    .Title(title)
                    .Photo(chatPhoto.ToReadOnlySpan())
                    .ParticipantsCount(participantIds.Count)
                    .Date(date)
                    .Version(1)
                    .DefaultBannedRights(defaultBannedRights)
                    .Build();
                chatBytes = chatToStore.AsSpan().ToArray();
                _chatRepository.PutChat(chatToStore);
            }

            using TLChatFullInfo fullInfo = ChatFullInfo.Builder()
                .ChatId(chatId)
                .About(ReadOnlySpan<byte>.Empty)
                .Build();
            _chatRepository.PutFullInfo(fullInfo);

            var participantInfos = new List<TLChatParticipantInfo>(participantIds.Count);
            foreach (long participantId in participantIds)
            {
                TLChatParticipantInfo participantInfo = ChatParticipantInfo.Builder()
                    .ChatId(chatId)
                    .UserId(participantId)
                    .Role(participantId == creatorUserId
                        ? (int)ChatParticipantRole.Creator
                        : (int)ChatParticipantRole.Member)
                    .InviterId(creatorUserId)
                    .Date(date)
                    .Build();
                participantInfos.Add(participantInfo);
                _chatParticipantsRepository.PutParticipant(participantInfo);
            }

            using (TLChatInviteInfo defaultInvite =
                   ChatInvites.CreateDefaultPermanentInvite(chatId, creatorUserId, date))
            {
                _chatInvitesRepository.PutInvite(defaultInvite);
            }

            using TLChatParticipants participants = BuildChatParticipants(chatId, participantInfos, 1);
            using TLPeer creatorPeer = new PeerUser(creatorUserId);
            using TLPeer chatPeer = new PeerChat(chatId);
            byte[] actionBytes;
            {
                var actionUsers = new VectorOfLong();
                foreach (long participantId in participantIds)
                {
                    actionUsers.Append(participantId);
                }

                using TLMessageAction action = MessageActionChatCreate.Builder()
                    .Title(title)
                    .Users(actionUsers)
                    .Build();
                actionBytes = action.AsSpan().ToArray();
            }

            var resultUpdateBytes = new List<byte[]>();
            var liveMessageUpdates = new List<(long ParticipantId, byte[] Bytes)>();
            var publicationContexts = new Dictionary<long, IUpdatesContext>();
            foreach (long participantId in participantIds)
            {
                IUpdatesContext participantCtx = participantId == creatorUserId
                    ? _updatesContextFactory.GetUpdatesContext(authKeyId, creatorUserId)
                    : _updatesContextFactory.GetUpdatesContext(null, participantId);
                await participantCtx.BeginPtsPublication();
                publicationContexts.Add(participantId, participantCtx);
            }

            byte[] participantUpdateBytes;
            try
            {
                foreach (long participantId in participantIds)
                {
                    IUpdatesContext participantCtx = publicationContexts[participantId];
                    int messageId = (int)await participantCtx.NextMessageId();
                    using TLMessage serviceMessage = MessageService.Builder()
                        .Id(messageId)
                        .OutProperty(participantId == creatorUserId)
                        .FromId(creatorPeer.AsSpan())
                        .PeerId(chatPeer.AsSpan())
                        .Date(date)
                        .Action(actionBytes)
                        .Build();
                    int pts = participantId == creatorUserId
                        ? await participantCtx.IncrementPts()
                        : await participantCtx.IncrementPtsForMessage(
                            (int)TLPeer.PeerType.PeerChat, chatId, messageId);
                    _messageRepository.PutMessage(participantId,
                        serviceMessage, pts);

                    using TLUpdate updateNewMessage = UpdateNewMessage.Builder()
                        .Message(serviceMessage.AsSpan())
                        .Pts(pts)
                        .PtsCount(1)
                        .Build();
                    byte[] updateBytes = updateNewMessage.AsSpan().ToArray();
                    if (participantId == creatorUserId)
                    {
                        long randomId;
                        do
                        {
                            randomId = Random.Shared.NextInt64();
                        } while (randomId == 0);

                        using TLUpdate updateMessageId = UpdateMessageID.Builder()
                            .Id(messageId)
                            .RandomId(randomId)
                            .Build();
                        resultUpdateBytes.Add(updateMessageId.AsSpan().ToArray());
                        resultUpdateBytes.Add(updateBytes);
                    }
                    liveMessageUpdates.Add((participantId, updateBytes));
                }

                using TLUpdate updateChatParticipants = UpdateChatParticipants.Builder()
                    .Participants(participants.AsSpan())
                    .Build();
                participantUpdateBytes = updateChatParticipants.AsSpan().ToArray();
                resultUpdateBytes.Add(participantUpdateBytes);

                await _unitOfWork.SaveAsync();
                foreach ((long participantId, byte[] updateBytes) in liveMessageUpdates)
                {
                    await _updates.EnqueueUpdate(participantId,
                        new TLUpdate(updateBytes, 0, updateBytes.Length));
                }
            }
            finally
            {
                foreach (IUpdatesContext context in publicationContexts.Values)
                {
                    await context.CompletePtsPublication();
                }
            }
            foreach (long participantId in participantIds)
            {
                await _updates.EnqueueUpdate(participantId,
                    new TLUpdate(participantUpdateBytes, 0, participantUpdateBytes.Length));
            }

            var seqCtx = _updatesContextFactory.GetUpdatesContext(authKeyId, creatorUserId);
            int seq = await seqCtx.IncrementSeq();
            var resultUpdates = new Vector();
            foreach (byte[] updateBytes in resultUpdateBytes)
            {
                resultUpdates.AppendTLObject(updateBytes);
            }

            var userVector = new Vector();
            AppendUsers(creatorUserId, ref userVector, participantIds.Concat(missingInviteeIds));
            var chatVector = new Vector();
            chatVector.AppendTLObject(chatBytes);
            using TLUpdates updates = Ferrite.TL.baseLayer.Updates.Builder()
                .UpdatesProperty(resultUpdates)
                .Users(userVector)
                .Chats(chatVector)
                .Date(date)
                .Seq(seq)
                .Build();

            _log.Debug($"👥 CreateChat creator:{creatorUserId} chat:{chatId} " +
                       $"users:{participantIds.Count} missing:{missingInviteeIds.Count}");
            var missingInvitees = new Vector();
            foreach (long missingInviteeId in missingInviteeIds)
            {
                using var missingInvitee = MissingInvitee.Builder()
                    .UserId(missingInviteeId)
                    .Build();
                missingInvitees.AppendTLObject(missingInvitee.ToReadOnlySpan());
            }

            var result = InvitedUsers.Builder()
                .Updates(updates.AsSpan())
                .MissingInvitees(missingInvitees)
                .Build();

            foreach (var participantInfo in participantInfos)
            {
                participantInfo.Dispose();
            }

            return result;
        }
}
