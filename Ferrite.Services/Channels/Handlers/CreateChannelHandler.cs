// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using System.Text.RegularExpressions;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.channels;
using Ferrite.TL.baseLayer.dto;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

public sealed class CreateChannelHandler : ChannelsHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChannelMessagesRepository _channelMessagesRepository;
    private readonly IChatInvitesRepository _chatInvitesRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IForumTopicsRepository _forumTopicsRepository;

    public CreateChannelHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChannelMessagesRepository channelMessagesRepository, IChatInvitesRepository chatInvitesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IForumTopicsRepository forumTopicsRepository, IMessageRepository messageRepository, IUserRepository userRepository, ICounterFactory counterFactory,
        IdAllocators ids, IUpdatesContextFactory updatesContextFactory,
        IUpdatesService updates, ISearchEngine search, IUploadService upload,
        IPhotoProcessingService photos, ILogger log, ChatRowStore chatRows,
        UpdateFanout fanout, ChatSettingsStore settings)
        : base(unitOfWork, channelMessagesRepository, authorizationRepository, channelAdminLogRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, counterFactory, ids, updatesContextFactory, updates,
            search, upload, photos, log, chatRows, fanout)
    {
        _authorizationRepository = authorizationRepository;
        _channelMessagesRepository = channelMessagesRepository;
        _chatInvitesRepository = chatInvitesRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;
        _forumTopicsRepository = forumTopicsRepository;

        _settings = settings;
    }

    private readonly ChatSettingsStore _settings;

    [TLFunction(Constructors.baseLayer_CreateChannel)]
    public async Task<Ferrite.TL.baseLayer.TLUpdates> Handle(long authKeyId, TLBytes q)
    {
        var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
        if (auth == null)
        {
            return (Ferrite.TL.baseLayer.TLUpdates)RpcErrorGenerator.GenerateError(400, "AUTH_KEY_INVALID"u8);
        }

        long creatorUserId = auth.Value.AsAuthInfo().UserId;
        var request = (CreateChannel)q;
        bool forum = request.Forum;
        // TDLib sends createChannel(forum:true) without also setting megagroup.
        // A forum is always a supergroup, so normalize the stored channel shape.
        bool broadcast = request.Broadcast && !forum;
        bool megagroup = request.Megagroup || forum;
        byte[] title = request.Title.ToArray();
        byte[] about = request.About.ToArray();
        int requestedTtlPeriod = request.Flags[4] ? request.TtlPeriod : 0;

        long channelId = await _ids.NextChatIdAsync();
        long accessHash;
        do
        {
            accessHash = Random.Shared.NextInt64();
        } while (accessHash == 0);

        int date = (int)DateTimeOffset.Now.ToUnixTimeSeconds();

        byte[] channelBytes;
        {
            using var chatPhoto = ChatPhotoEmpty.Builder().Build();
            // An empty ban set, sent rather than omitted: the pinned client reads
            // an absent default_banned_rights as permitting NOTHING, which leaves
            // every plain member unable to post. See
            // ChatRights.BuildUnrestrictedDefaultBannedRights.
            byte[] defaultBannedRights =
                ChatRights.BuildUnrestrictedDefaultBannedRights();
            var channelBuilder = Channel.Builder()
                .Creator(true)
                .Id(channelId)
                .AccessHash(accessHash)
                .Title(title)
                .Photo(chatPhoto.ToReadOnlySpan())
                .Date(date)
                .ParticipantsCount(1)
                .DefaultBannedRights(defaultBannedRights);
            if (broadcast)
            {
                channelBuilder = channelBuilder.Broadcast(true);
            }
            if (megagroup)
            {
                channelBuilder = channelBuilder.Megagroup(true);
            }
            if (forum)
            {
                channelBuilder = channelBuilder.Forum(true);
            }

            using TLChat channelToStore = channelBuilder.Build();
            channelBytes = channelToStore.AsSpan().ToArray();
            _chatRepository.PutChat(channelToStore);
        }

        using TLChatFullInfo fullInfo = ChatFullInfo.Builder()
            .ChatId(channelId)
            .About(about)
            .Build();
        _chatRepository.PutFullInfo(fullInfo);

        using TLChatParticipantInfo creatorParticipant = ChatParticipantInfo.Builder()
            .ChatId(channelId)
            .UserId(creatorUserId)
            .Role((int)ChatParticipantRole.Creator)
            .InviterId(creatorUserId)
            .Date(date)
            .Build();
        _chatParticipantsRepository.PutParticipant(creatorParticipant);

        // Newly created channels already have a default permanent invite link.
        using (TLChatInviteInfo defaultInvite =
               ChatInvites.CreateDefaultPermanentInvite(channelId, creatorUserId, date))
        {
            _chatInvitesRepository.PutInvite(defaultInvite);
        }

        // Channel box: message id starts at 1, pts is seeded to 1 and advances to 2
        // for the creation event, matching the "pts starts at 1" convention.
        var channelBox = new ChannelMessageBox(_counterFactory, channelId);
        int messageId = await channelBox.NextMessageId();
        using TLPeer channelPeer = new PeerChannel(channelId);
        using TLPeer creatorPeer = new PeerUser(creatorUserId);
        byte[] serviceMessageBytes;
        int creationPts;
        {
            using TLMessageAction action = MessageActionChannelCreate.Builder()
                .Title(title)
                .Build();
            using TLMessage serviceMessage = MessageService.Builder()
                .Id(messageId)
                .FromId(creatorPeer.AsSpan())
                .PeerId(channelPeer.AsSpan())
                .Date(date)
                .Action(action.AsSpan())
                .Build();
            serviceMessageBytes = serviceMessage.AsSpan().ToArray();
            creationPts = await channelBox.IncrementPts();
            _channelMessagesRepository.PutMessage(channelId, serviceMessage, creationPts);
        }

        // The creating client carries its own default auto-delete period in the
        // request, which is how a new channel inherits it; the account-wide default
        // never rewrites a conversation that already exists.
        if (requestedTtlPeriod > 0)
        {
            _settings.Put(ChatSettingsScope.ForChannel(channelId),
                ChatSettingsSnapshot.Empty with { TtlPeriod = requestedTtlPeriod });
        }

        if (forum)
        {
            using TLForumTopicInfo generalTopic = ForumMessages.BuildStoredForumTopic(channelId, 1,
                creatorUserId, date, "General"u8.ToArray(), 0x6FB9F0, 0, messageId,
                closed: false, hidden: false, pinnedOrder: 0);
            _forumTopicsRepository.PutTopic(generalTopic);
        }

        await _unitOfWork.SaveAsync();

        var seqCtx = _updatesContextFactory.GetUpdatesContext(authKeyId, creatorUserId);
        int seq = await seqCtx.IncrementSeq();

        var resultUpdates = new Vector();
        using (TLUpdate updateChannel = UpdateChannel.Builder().ChannelId(channelId).Build())
        {
            resultUpdates.AppendTLObject(updateChannel.AsSpan());
        }

        // TDLib's on_create_new_dialog (CreateChannelQuery) rejects the result unless the
        // creation service message is paired with an updateMessageID carrying a nonzero
        // random_id (get_sent_messages_random_ids must return exactly one), mirroring the
        // basic-group createChat requirement.
        long randomId;
        do
        {
            randomId = Random.Shared.NextInt64();
        } while (randomId == 0);

        using (TLUpdate updateMessageId = UpdateMessageID.Builder()
                   .Id(messageId)
                   .RandomId(randomId)
                   .Build())
        {
            resultUpdates.AppendTLObject(updateMessageId.AsSpan());
        }
        using (TLUpdate updateNewChannelMessage = UpdateNewChannelMessage.Builder()
                   .Message(serviceMessageBytes)
                   .Pts(creationPts)
                   .PtsCount(1)
                   .Build())
        {
            resultUpdates.AppendTLObject(updateNewChannelMessage.AsSpan());
        }

        var userVector = new Vector();
        AppendUser(ref userVector, creatorUserId);
        var chatVector = new Vector();
        chatVector.AppendTLObject(channelBytes);

        _log.Debug($"📣 CreateChannel creator:{creatorUserId} channel:{channelId} " +
                   $"broadcast:{broadcast} megagroup:{megagroup} forum:{forum} pts:{creationPts}");

        return Ferrite.TL.baseLayer.Updates.Builder()
            .UpdatesProperty(resultUpdates)
            .Users(userVector)
            .Chats(chatVector)
            .Date(date)
            .Seq(seq)
            .Build();
    }
}
