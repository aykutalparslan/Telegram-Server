// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public class ForumTopicsRepository : IForumTopicsRepository
{
    private readonly IKVStore _topics;
    private readonly IKVStore _readStates;
    private readonly IKVStore _viewStates;

    public ForumTopicsRepository(IKVStore topics, IKVStore readStates, IKVStore viewStates)
    {
        _topics = topics;
        topics.SetSchema(new TableDefinition("ferrite", "forum_topics",
            new KeyDefinition("pk",
                new DataColumn { Name = "channel_id", Type = DataType.Long },
                new DataColumn { Name = "topic_id", Type = DataType.Int })));
        _readStates = readStates;
        readStates.SetSchema(new TableDefinition("ferrite", "forum_topic_read_states",
            new KeyDefinition("pk",
                new DataColumn { Name = "channel_id", Type = DataType.Long },
                new DataColumn { Name = "topic_id", Type = DataType.Int },
                new DataColumn { Name = "user_id", Type = DataType.Long })));
        _viewStates = viewStates;
        viewStates.SetSchema(new TableDefinition("ferrite", "forum_view_states",
            new KeyDefinition("pk",
                new DataColumn { Name = "channel_id", Type = DataType.Long },
                new DataColumn { Name = "user_id", Type = DataType.Long })));
    }

    public bool PutTopic(TLForumTopicInfo topic)
    {
        var info = topic.AsForumTopicInfo();
        return _topics.Put(topic.AsSpan().ToArray(), info.ChannelId, info.TopicId);
    }

    public async ValueTask<TLForumTopicInfo?> GetTopicAsync(long channelId, int topicId)
    {
        byte[]? bytes = await _topics.GetAsync(channelId, topicId);
        return bytes is { Length: > 0 }
            ? new TLForumTopicInfo(bytes, 0, bytes.Length)
            : null;
    }

    public async ValueTask<IReadOnlyCollection<TLForumTopicInfo>> GetTopicsAsync(long channelId)
    {
        List<TLForumTopicInfo> topics = new();
        await foreach (byte[] bytes in _topics.IterateAsync(channelId))
        {
            topics.Add(new TLForumTopicInfo(bytes, 0, bytes.Length));
        }
        return topics;
    }

    public bool DeleteTopic(long channelId, int topicId)
    {
        bool deleted = _topics.Delete(channelId, topicId);
        _readStates.Delete(channelId, topicId);
        return deleted;
    }

    public bool DeleteTopics(long channelId)
    {
        bool deleted = _topics.Delete(channelId);
        _readStates.Delete(channelId);
        _viewStates.Delete(channelId);
        return deleted;
    }

    public bool PutReadState(TLForumTopicReadState readState)
    {
        var state = readState.AsForumTopicReadState();
        return _readStates.Put(readState.AsSpan().ToArray(), state.ChannelId,
            state.TopicId, state.UserId);
    }

    public async ValueTask<TLForumTopicReadState?> GetReadStateAsync(long channelId,
        int topicId, long userId)
    {
        byte[]? bytes = await _readStates.GetAsync(channelId, topicId, userId);
        return bytes is { Length: > 0 }
            ? new TLForumTopicReadState(bytes, 0, bytes.Length)
            : null;
    }

    public bool DeleteReadStates(long channelId, int topicId) =>
        _readStates.Delete(channelId, topicId);

    public bool PutUserState(TLForumUserState userState)
    {
        var state = userState.AsForumUserState();
        return _viewStates.Put(userState.AsSpan().ToArray(), state.ChannelId, state.UserId);
    }

    public async ValueTask<TLForumUserState?> GetUserStateAsync(long channelId, long userId)
    {
        byte[]? bytes = await _viewStates.GetAsync(channelId, userId);
        return bytes is { Length: > 0 }
            ? new TLForumUserState(bytes, 0, bytes.Length)
            : null;
    }
}
