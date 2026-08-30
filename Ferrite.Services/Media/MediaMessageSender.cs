// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL.baseLayer;

namespace Ferrite.Services.Media;

public sealed class MediaMessageSender
{
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IForumTopicsRepository _forumTopicsRepository;
    private readonly IMessagingSettingsRepository _messagingSettingsRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly SendPipeline _send;
    private readonly UpdateFanout _fanout;

    public MediaMessageSender(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IForumTopicsRepository forumTopicsRepository, IMessagingSettingsRepository messagingSettingsRepository, SendPipeline send,
        UpdateFanout fanout)
    {
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;
        _forumTopicsRepository = forumTopicsRepository;
        _messagingSettingsRepository = messagingSettingsRepository;

        _unitOfWork = unitOfWork;
        _send = send;
        _fanout = fanout;
    }

    public Task<PreparedMessageTarget> PrepareAsync(long userId,
        TLPeer.PeerType peerType, long peerId, byte[] requestBytes,
        IReadOnlyCollection<MediaResolver.MediaCategory> categories)
    {
        ChatBannedAction[] actions = categories.Distinct().Select(ToBannedAction)
            .ToArray();
        return MessageSendTargetResolver.PrepareAsync(_chatRepository, _chatParticipantsRepository, _forumTopicsRepository, _messagingSettingsRepository, userId, peerType,
            peerId, requestBytes, actions);
    }

    public async Task<MediaSentBatch> SendAsync(long authKeyId, long userId,
        PreparedMessageTarget target, byte[] requestBytes, byte[] mediaBytes,
        long groupedId = 0)
    {
        if (target.PeerType == TLPeer.PeerType.PeerChannel)
        {
            ChannelSentBatch sent = await _send.SendChannelMessageAsync(userId,
                target.PeerId, target.Sender, target.Broadcast,
                target.ForumTopicId, target.ForumTopic, requestBytes,
                target.ChatBytes!, mediaBytes, groupedId);
            return new MediaSentBatch(sent.UserId, TLPeer.PeerType.PeerChannel,
                sent.ChannelId, sent.RandomId, sent.Id, sent.Pts, sent.Date,
                sent.MessageBytes, sent.ChannelBytes);
        }

        ShortSentBatch common = target.PeerType == TLPeer.PeerType.PeerChat
            ? await _send.SendBasicGroupMessageAsync(authKeyId, userId, target.PeerId,
                target.RelatedUserIds, requestBytes, mediaBytes, groupedId,
                target.ChatBytes)
            : await _send.SendPrivateMessageAsync(authKeyId, userId, target.PeerType,
                target.PeerId, requestBytes, mediaBytes, groupedId);
        return new MediaSentBatch(common.UserId, common.PeerType, common.PeerId,
            common.RandomId, common.Id, common.Pts, common.Date,
            common.MessageBytes, common.ChatBytes);
    }

    public Task<TLUpdates> BuildAlbumResultAsync(long authKeyId, long userId,
        PreparedMessageTarget target, IReadOnlyList<MediaSentBatch> sentItems) =>
        _fanout.BuildMediaAlbumSentResultAsync(authKeyId, userId, sentItems,
            target.RelatedUserIds);

    public Task<TLUpdates> BuildChannelResultAsync(long authKeyId,
        MediaSentBatch sent) => _fanout.BuildChannelSentResultAsync(authKeyId,
        new ChannelSentBatch(sent.UserId, sent.PeerId, sent.RandomId, sent.Id,
            sent.Pts, sent.Date, sent.MessageBytes, sent.ChatBytes!));

    private static ChatBannedAction ToBannedAction(
        MediaResolver.MediaCategory category) => category switch
    {
        MediaResolver.MediaCategory.Photo => ChatBannedAction.SendPhotos,
        MediaResolver.MediaCategory.Document => ChatBannedAction.SendDocuments,
        MediaResolver.MediaCategory.Poll => ChatBannedAction.SendPolls,
        _ => ChatBannedAction.SendMessages,
    };
}
