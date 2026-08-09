// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public interface IForumTopicsRepository
{
    bool PutTopic(TLForumTopicInfo topic);
    ValueTask<TLForumTopicInfo?> GetTopicAsync(long channelId, int topicId);
    ValueTask<IReadOnlyCollection<TLForumTopicInfo>> GetTopicsAsync(long channelId);
    bool DeleteTopic(long channelId, int topicId);
    bool DeleteTopics(long channelId);
    bool PutReadState(TLForumTopicReadState readState);
    ValueTask<TLForumTopicReadState?> GetReadStateAsync(long channelId, int topicId,
        long userId);
    bool DeleteReadStates(long channelId, int topicId);
    bool PutUserState(TLForumUserState userState);
    ValueTask<TLForumUserState?> GetUserStateAsync(long channelId, long userId);
}
