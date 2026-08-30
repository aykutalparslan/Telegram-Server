// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.channels;
using Ferrite.TL.baseLayer.dto;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.ChannelForums;

public sealed class CreateForumTopicHandler
{
    private readonly IMessagingSettingsRepository _messagingSettingsRepository;

    private readonly IChannelMessagesRepository _channelMessagesRepository;

    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;

    private readonly IForumTopicsRepository _forumTopicsRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICounterFactory _counterFactory;
    private readonly ILogger _log;
    private readonly UpdateFanout _fanout;

    public CreateForumTopicHandler(IUnitOfWork unitOfWork, IMessagingSettingsRepository messagingSettingsRepository, IChannelMessagesRepository channelMessagesRepository, IAuthorizationRepository authorizationRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IForumTopicsRepository forumTopicsRepository,
        ICounterFactory counterFactory, ILogger log, UpdateFanout fanout)
    {
        _messagingSettingsRepository = messagingSettingsRepository;

        _channelMessagesRepository = channelMessagesRepository;

        _authorizationRepository = authorizationRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;

        _forumTopicsRepository = forumTopicsRepository;

        _unitOfWork = unitOfWork;
        _counterFactory = counterFactory;
        _log = log;
        _fanout = fanout;
    }

    [TLFunction(Constructors.baseLayer_CreateForumTopic)]
    public async Task<Ferrite.TL.baseLayer.TLUpdates> Handle(long authKeyId, TLBytes q)
    {
        var request = (CreateForumTopic)q;
        long? channelId = ChannelForumAccess.ResolveInputChannelId(request.Get_ChannelView());
        byte[] title = request.Title.ToArray();
        int iconColor = request.IconColor == 0 ? 0x6FB9F0 : request.IconColor;
        long iconEmojiId = request.IconEmojiId;
        long randomId = request.RandomId;
        if (title.Length == 0)
            return ChannelForumErrors.Updates("TOPIC_TITLE_EMPTY"u8);

        var (currentUserId, channelBytes, error) =
            await ChannelForumAccess.PrepareForumMutationAsync(_authorizationRepository, _chatRepository, _chatParticipantsRepository, authKeyId, channelId, ChatAdminRightRequirement.ManageTopics);
        if (error != null)
            return ChannelForumErrors.Updates(Encoding.UTF8.GetBytes(error));

        var sendAsRequest = (CreateForumTopic)q;
        bool hasExplicitSender = sendAsRequest.Flags[2];
        DialogPeerKey? explicitSender = hasExplicitSender
            ? PeerResolver.ResolveOptionalDialogPeer(
                sendAsRequest.Get_SendAsView(), currentUserId)
            : null;
        SendAsResolver.Resolution sendAs = await SendAsResolver.ResolveAsync(_messagingSettingsRepository, _chatParticipantsRepository, _chatRepository, currentUserId,
            new DialogPeerKey(TLPeer.PeerType.PeerChannel, channelId!.Value),
            hasExplicitSender, explicitSender);
        if (sendAs.Error != null)
            return ChannelForumErrors.Updates(Encoding.UTF8.GetBytes(sendAs.Error));

        byte[] actionBytes;
        {
            var actionBuilder = MessageActionTopicCreate.Builder()
                .Title(title)
                .IconColor(iconColor);
            if (iconEmojiId != 0) actionBuilder = actionBuilder.IconEmojiId(iconEmojiId);
            using TLMessageAction action = actionBuilder.Build();
            actionBytes = action.AsSpan().ToArray();
        }

        int date = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
        long id = channelId!.Value;
        var (serviceMessageBytes, pts) =
            await ChannelForumUpdates.WriteChannelServiceMessageAsync(_channelMessagesRepository, _counterFactory, id, currentUserId, actionBytes, date,
                sender: sendAs.Sender);
        int topicId = ((MessageService)serviceMessageBytes.AsSpan()).Id;
        using (TLForumTopicInfo topic = ForumMessages.BuildStoredForumTopic(id, topicId,
                   currentUserId, date, title, iconColor, iconEmojiId, topicId,
                   closed: false, hidden: false, pinnedOrder: 0))
        {
            _forumTopicsRepository.PutTopic(topic);
        }

        byte[] updateMessageIdBytes;
        byte[] updateNewMessageBytes;
        using (TLUpdate updateMessageId = UpdateMessageID.Builder()
                   .Id(topicId).RandomId(randomId).Build())
        {
            updateMessageIdBytes = updateMessageId.AsSpan().ToArray();
        }
        using (TLUpdate updateNewMessage = UpdateNewChannelMessage.Builder()
                   .Message(serviceMessageBytes).Pts(pts).PtsCount(1).Build())
        {
            updateNewMessageBytes = updateNewMessage.AsSpan().ToArray();
        }

        await _fanout.PushChannelServiceMessageAsync(id, currentUserId,
            serviceMessageBytes, pts);
        _log.Debug($"📣 CreateForumTopic user:{currentUserId} channel:{id} topic:{topicId} pts:{pts}");
        return await ChannelForumUpdates.BuildForumResultAsync(_unitOfWork, _fanout,
            authKeyId, currentUserId, channelBytes,
            new[] { updateMessageIdBytes, updateNewMessageBytes },
            sendAs.Sender.Type == TLPeer.PeerType.PeerChannel
                ? new[] { sendAs.Sender.Id }
                : Array.Empty<long>());
    }
}
