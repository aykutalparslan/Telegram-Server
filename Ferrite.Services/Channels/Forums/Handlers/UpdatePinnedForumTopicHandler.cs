// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.channels;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Handlers.ChannelForums;

public sealed class UpdatePinnedForumTopicHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;

    private readonly IForumTopicsRepository _forumTopicsRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly UpdateFanout _fanout;

    public UpdatePinnedForumTopicHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IForumTopicsRepository forumTopicsRepository, UpdateFanout fanout)
    {
        _authorizationRepository = authorizationRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;

        _forumTopicsRepository = forumTopicsRepository;

        _unitOfWork = unitOfWork;
        _fanout = fanout;
    }

    [TLFunction(Constructors.baseLayer_UpdatePinnedForumTopic)]
    public async Task<Ferrite.TL.baseLayer.TLUpdates> Handle(long authKeyId, TLBytes q)
    {
        var request = (UpdatePinnedForumTopic)q;
        long? channelId = ChannelForumAccess.ResolveInputChannelId(request.Get_ChannelView());
        int topicId = request.TopicId;
        bool pinned = request.Pinned;
        var (currentUserId, channelBytes, error) =
            await ChannelForumAccess.PrepareForumMutationAsync(_authorizationRepository, _chatRepository, _chatParticipantsRepository, authKeyId, channelId, ChatAdminRightRequirement.ManageTopics);
        if (error != null)
            return ChannelForumErrors.Updates(Encoding.UTF8.GetBytes(error));

        using var stored = await _forumTopicsRepository
            .GetTopicAsync(channelId!.Value, topicId);
        if (stored == null) return ChannelForumErrors.Updates("TOPIC_ID_INVALID"u8);
        StoredMessageForumTopic topic = ForumMessages.SnapshotMessageForumTopic(stored.Value);
        if ((topic.PinnedOrder > 0) == pinned)
            return ChannelForumErrors.Updates("TOPIC_NOT_MODIFIED"u8);

        int pinnedOrder = 0;
        if (pinned)
        {
            var all = await ChannelForumTopics.SnapshotAsync(_forumTopicsRepository, channelId.Value);
            pinnedOrder = all.Count == 0 ? 1 : all.Max(x => x.PinnedOrder) + 1;
        }
        using (TLForumTopicInfo updated = ForumMessages.BuildStoredForumTopic(topic.ChannelId,
                   topic.TopicId, topic.CreatorId, topic.Date, topic.Title, topic.IconColor,
                   topic.IconEmojiId, topic.TopMessage, topic.Closed, topic.Hidden, pinnedOrder))
        {
            _forumTopicsRepository.PutTopic(updated);
        }

        byte[] updateBytes;
        var updateBuilder = UpdateChannelPinnedTopic.Builder()
            .ChannelId(channelId.Value).TopicId(topicId);
        if (pinned) updateBuilder = updateBuilder.Pinned(true);
        using (TLUpdate update = updateBuilder.Build())
        {
            updateBytes = update.AsSpan().ToArray();
        }
        await _fanout.PushSerializedToOtherChannelMembersAsync(channelId.Value,
            currentUserId, new[] { updateBytes });
        return await ChannelForumUpdates.BuildForumResultAsync(_unitOfWork, _fanout,
            authKeyId, currentUserId, channelBytes, new[] { updateBytes });
    }
}
