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

public sealed class MigrateChatHandler : MessagesHandlerBase
{
    private readonly IChannelMessagesRepository _channelMessagesRepository;
    private readonly IChatInvitesRepository _chatInvitesRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IMessageRepository _messageRepository;

    public MigrateChatHandler(IUnitOfWork unitOfWork, IForumTopicsRepository forumTopicsRepository, IMessagingSettingsRepository messagingSettingsRepository, IAuthorizationRepository authorizationRepository, IChannelMessagesRepository channelMessagesRepository, IChatInvitesRepository chatInvitesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,ISearchEngine search,
        IUpdatesService updates, IUpdatesContextFactory updatesContextFactory,
        ILogger log, IUploadService upload, IPhotoProcessingService photos,
        ICounterFactory counterFactory, IdAllocators ids,
        ChatRowStore chatRows, InviteStore invites,
        PrivacyEvaluator privacy, MessageStore messages, SendPipeline send,
        UpdateFanout fanout, DialogBuilder dialogs)
        : base(unitOfWork, forumTopicsRepository, messagingSettingsRepository, authorizationRepository, channelMessagesRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, search, updates, updatesContextFactory, log, upload, photos, counterFactory, ids, chatRows, invites, privacy, messages, send, fanout, dialogs)
    {
        _channelMessagesRepository = channelMessagesRepository;
        _chatInvitesRepository = chatInvitesRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;
        _messageRepository = messageRepository;

    }

    [TLFunction(Constructors.baseLayer_MigrateChat)]
    public async Task<TLUpdates> Handle(long authKeyId, TLBytes q)
        {
            long chatId = ((MigrateChat)q).ChatId;
            var (context, error) = await PrepareBasicChatMutation(authKeyId, chatId,
                requireAdmin: true, requireCreator: true);
            if (error != null)
            {
                return ErrorUpdates(error);
            }

            try
            {
                byte[] title;
                byte[] photo;
                byte[] defaultBannedRights;
                bool noforwards;
                int oldVersion;
                {
                    using var oldChat = new TLChat(context.ChatBytes, 0, context.ChatBytes.Length);
                    var chat = oldChat.AsChat();
                    title = chat.Title.ToArray();
                    photo = chat.Photo.ToArray();
                    defaultBannedRights = chat.Flags[18]
                        ? chat.DefaultBannedRights.ToArray()
                        : Array.Empty<byte>();
                    noforwards = chat.Noforwards;
                    oldVersion = chat.Version;
                }

                byte[] about = Array.Empty<byte>();
                using (var oldFullInfo = await _chatRepository.GetFullInfoAsync(chatId))
                {
                    if (oldFullInfo != null)
                    {
                        about = oldFullInfo.Value.AsChatFullInfo().About.ToArray();
                    }
                }

                long channelId = await _ids.NextChatIdAsync();
                long accessHash;
                do
                {
                    accessHash = Random.Shared.NextInt64();
                } while (accessHash == 0);

                int date = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
                byte[] oldChatBytes;
                using (var migratedTo = InputChannel.Builder()
                           .ChannelId(channelId)
                           .AccessHash(accessHash)
                           .Build())
                using (var oldChat = new TLChat(context.ChatBytes, 0, context.ChatBytes.Length))
                using (TLChat migratedChat = oldChat.AsChat().Clone()
                           .Deactivated(true)
                           .MigratedTo(migratedTo.ToReadOnlySpan())
                           .Version(oldVersion + 1)
                           .Build())
                {
                    oldChatBytes = migratedChat.AsSpan().ToArray();
                    _chatRepository.PutChat(migratedChat);
                }

                byte[] channelBytes;
                {
                    var channelBuilder = Channel.Builder()
                        .Creator(true)
                        .Megagroup(true)
                        .Noforwards(noforwards)
                        .Id(channelId)
                        .AccessHash(accessHash)
                        .Title(title)
                        .Photo(photo)
                        .Date(date)
                        .ParticipantsCount(context.ActiveParticipants.Count);
                    if (defaultBannedRights.Length > 0)
                    {
                        channelBuilder = channelBuilder.DefaultBannedRights(defaultBannedRights);
                    }

                    using TLChat channel = channelBuilder.Build();
                    channelBytes = channel.AsSpan().ToArray();
                    _chatRepository.PutChat(channel);
                }

                foreach (var participantInfo in context.ActiveParticipants)
                {
                    using TLChatParticipantInfo migratedParticipant = participantInfo
                        .AsChatParticipantInfo()
                        .Clone()
                        .ChatId(channelId)
                        .Build();
                    _chatParticipantsRepository.PutParticipant(migratedParticipant);
                }

                // A migrated megagroup is a newly created channel and therefore gets the
                // same default permanent invite invariant as channels.createChannel.
                using (TLChatInviteInfo defaultInvite =
                       ChatInvites.CreateDefaultPermanentInvite(channelId,
                           context.CurrentUserId, date))
                {
                    _chatInvitesRepository.PutInvite(defaultInvite);
                }

                byte[] migrateToActionBytes;
                using (TLMessageAction action = MessageActionChatMigrateTo.Builder()
                           .ChannelId(channelId)
                           .Build())
                {
                    migrateToActionBytes = action.AsSpan().ToArray();
                }

                using TLPeer actorPeer = new PeerUser(context.CurrentUserId);
                using TLPeer oldChatPeer = new PeerChat(chatId);
                var oldMigrationUpdates = new List<(long ParticipantId, byte[] UpdateBytes)>();
                byte[] actorMigrationUpdateBytes = Array.Empty<byte>();
                int migratedFromMaxId = 0;
                foreach (var participantInfo in context.ActiveParticipants)
                {
                    long participantId = participantInfo.AsChatParticipantInfo().UserId;
                    IUpdatesContext participantContext = participantId == context.CurrentUserId
                        ? _updatesContextFactory.GetUpdatesContext(authKeyId, context.CurrentUserId)
                        : _updatesContextFactory.GetUpdatesContext(null, participantId);
                    int messageId = (int)await participantContext.NextMessageId();
                    using TLMessage migrationMessage = MessageService.Builder()
                        .Id(messageId)
                        .OutProperty(participantId == context.CurrentUserId)
                        .FromId(actorPeer.AsSpan())
                        .PeerId(oldChatPeer.AsSpan())
                        .Date(date)
                        .Action(migrateToActionBytes)
                        .Build();
                    int pts = participantId == context.CurrentUserId
                        ? await participantContext.IncrementPts()
                        : await participantContext.IncrementPtsForMessage(
                            (int)TLPeer.PeerType.PeerChat, chatId, messageId);
                    _messageRepository.PutMessage(participantId, migrationMessage, pts);

                    using TLUpdate migrationUpdate = UpdateNewMessage.Builder()
                        .Message(migrationMessage.AsSpan())
                        .Pts(pts)
                        .PtsCount(1)
                        .Build();
                    byte[] updateBytes = migrationUpdate.AsSpan().ToArray();
                    oldMigrationUpdates.Add((participantId, updateBytes));
                    if (participantId == context.CurrentUserId)
                    {
                        migratedFromMaxId = messageId;
                        actorMigrationUpdateBytes = updateBytes;
                    }
                }

                byte[] migrateFromActionBytes;
                using (TLMessageAction action = MessageActionChannelMigrateFrom.Builder()
                           .Title(title)
                           .ChatId(chatId)
                           .Build())
                {
                    migrateFromActionBytes = action.AsSpan().ToArray();
                }

                var channelBox = new ChannelMessageBox(_counterFactory, channelId);
                int channelMessageId = await channelBox.NextMessageId();
                byte[] channelMigrationMessageBytes;
                int channelPts;
                using (TLPeer channelPeer = new PeerChannel(channelId))
                using (TLMessage channelMigrationMessage = MessageService.Builder()
                           .Id(channelMessageId)
                           .FromId(actorPeer.AsSpan())
                           .PeerId(channelPeer.AsSpan())
                           .Date(date)
                           .Action(migrateFromActionBytes)
                           .Build())
                {
                    channelMigrationMessageBytes = channelMigrationMessage.AsSpan().ToArray();
                    channelPts = await channelBox.IncrementPts();
                    _channelMessagesRepository.PutMessage(channelId,
                        channelMigrationMessage, channelPts);
                }

                byte[] channelMigrationUpdateBytes;
                using (TLUpdate channelMigrationUpdate = UpdateNewChannelMessage.Builder()
                           .Message(channelMigrationMessageBytes)
                           .Pts(channelPts)
                           .PtsCount(1)
                           .Build())
                {
                    channelMigrationUpdateBytes = channelMigrationUpdate.AsSpan().ToArray();
                }

                using (TLChatFullInfo channelFullInfo = ChatFullInfo.Builder()
                           .ChatId(channelId)
                           .About(about)
                           .MigratedFromChatId(chatId)
                           .MigratedFromMaxId(migratedFromMaxId)
                           .Build())
                {
                    _chatRepository.PutFullInfo(channelFullInfo);
                }

                await _unitOfWork.SaveAsync();

                // Both service messages are relevant to every active member. The update
                // hydration layer includes both compact rows because each migration action
                // references the peer on the other side of the boundary.
                foreach (var (participantId, updateBytes) in oldMigrationUpdates)
                {
                    await _updates.EnqueueUpdate(participantId,
                        new TLUpdate(updateBytes, 0, updateBytes.Length));
                    await _updates.EnqueueUpdate(participantId,
                        new TLUpdate(channelMigrationUpdateBytes, 0,
                            channelMigrationUpdateBytes.Length));
                }

                var resultUpdates = new Vector();
                using (TLUpdate updateChannel = UpdateChannel.Builder()
                           .ChannelId(channelId)
                           .Build())
                {
                    resultUpdates.AppendTLObject(updateChannel.AsSpan());
                }
                resultUpdates.AppendTLObject(actorMigrationUpdateBytes);
                resultUpdates.AppendTLObject(channelMigrationUpdateBytes);

                var userVector = new Vector();
                AppendUsers(ref userVector, context.ActiveParticipants
                    .Select(p => p.AsChatParticipantInfo().UserId));
                var chatVector = new Vector();
                chatVector.AppendTLObject(oldChatBytes);
                chatVector.AppendTLObject(channelBytes);

                _log.Debug($"👥 MigrateChat creator:{context.CurrentUserId} chat:{chatId} " +
                           $"channel:{channelId} users:{context.ActiveParticipants.Count} " +
                           $"oldMaxId:{migratedFromMaxId} channelPts:{channelPts}");
                // Live enqueues above already advance per-session seq, so keep the direct
                // RPC result outside the seq sequence (the established group-mutation rule).
                return Updates.Builder()
                    .UpdatesProperty(resultUpdates)
                    .Users(userVector)
                    .Chats(chatVector)
                    .Date(date)
                    .Seq(0)
                    .Build();
            }
            finally
            {
                DisposeParticipants(context.ActiveParticipants);
            }
        }
}
