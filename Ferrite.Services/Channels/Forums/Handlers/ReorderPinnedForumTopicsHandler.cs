// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.channels;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Handlers.ChannelForums;

public sealed class ReorderPinnedForumTopicsHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;

    private readonly IForumTopicsRepository _forumTopicsRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly UpdateFanout _fanout;

    public ReorderPinnedForumTopicsHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IForumTopicsRepository forumTopicsRepository, UpdateFanout fanout)
    {
        _authorizationRepository = authorizationRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;

        _forumTopicsRepository = forumTopicsRepository;

        _unitOfWork = unitOfWork;
        _fanout = fanout;
    }

    [TLFunction(Constructors.baseLayer_ReorderPinnedForumTopics)]
    public async Task<Ferrite.TL.baseLayer.TLUpdates> Handle(long authKeyId, TLBytes q)
    {
        var request = (ReorderPinnedForumTopics)q;
        long? channelId = ChannelForumAccess.ResolveInputChannelId(request.Get_ChannelView());
        var vector = request.Order;
        List<int> order = new List<int>(vector.Count);
        for (int i = 0; i < vector.Count; i++) order.Add(vector[i]);
        var (currentUserId, channelBytes, error) =
            await ChannelForumAccess.PrepareForumMutationAsync(_authorizationRepository, _chatRepository, _chatParticipantsRepository, authKeyId, channelId, ChatAdminRightRequirement.ManageTopics);
        if (error != null)
            return ChannelForumErrors.Updates(Encoding.UTF8.GetBytes(error));

        List<StoredMessageForumTopic> topics =
            await ChannelForumTopics.SnapshotAsync(_forumTopicsRepository, channelId!.Value);
        var byId = topics.ToDictionary(x => x.TopicId);
        if (order.Distinct().Count() != order.Count ||
            order.Any(id => !byId.ContainsKey(id)))
            return ChannelForumErrors.Updates("TOPIC_ID_INVALID"u8);
        for (int i = 0; i < order.Count; i++)
        {
            StoredMessageForumTopic topic = byId[order[i]];
            int pinnedOrder = order.Count - i;
            using TLForumTopicInfo updated = ForumMessages.BuildStoredForumTopic(topic.ChannelId,
                topic.TopicId, topic.CreatorId, topic.Date, topic.Title, topic.IconColor,
                topic.IconEmojiId, topic.TopMessage, topic.Closed, topic.Hidden, pinnedOrder);
            _forumTopicsRepository.PutTopic(updated);
        }
        foreach (StoredMessageForumTopic topic in topics.Where(x => !order.Contains(x.TopicId)))
        {
            if (topic.PinnedOrder == 0) continue;
            using TLForumTopicInfo updated = ForumMessages.BuildStoredForumTopic(topic.ChannelId,
                topic.TopicId, topic.CreatorId, topic.Date, topic.Title, topic.IconColor,
                topic.IconEmojiId, topic.TopMessage, topic.Closed, topic.Hidden, 0);
            _forumTopicsRepository.PutTopic(updated);
        }

        byte[] updateBytes;
        var orderVector = new VectorOfInt();
        foreach (int id in order) orderVector.Append(id);
        using (TLUpdate update = UpdateChannelPinnedTopics.Builder()
                   .ChannelId(channelId.Value).Order(orderVector).Build())
        {
            updateBytes = update.AsSpan().ToArray();
        }
        await _fanout.PushSerializedToOtherChannelMembersAsync(channelId.Value,
            currentUserId, new[] { updateBytes });
        return await ChannelForumUpdates.BuildForumResultAsync(_unitOfWork, _fanout,
            authKeyId, currentUserId, channelBytes, new[] { updateBytes });
    }
}
