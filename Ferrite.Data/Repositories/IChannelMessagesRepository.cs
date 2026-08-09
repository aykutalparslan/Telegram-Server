// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public interface IChannelMessagesRepository
{
    public bool PutMessage(long channelId, TLMessage message, int pts);
    public ValueTask<TLSavedMessage?> GetMessageAsync(long channelId, int messageId);
    /// <summary>
    /// Returns the messages with ids in [minId, maxId] ordered by descending
    /// message id. A bound of 0 means unbounded.
    /// </summary>
    public ValueTask<IReadOnlyCollection<TLSavedMessage>> GetMessagesAsync(long channelId,
        int minId = 0, int maxId = 0);
    /// <summary>
    /// Returns the messages with pts in [minPts, maxPts] ordered by ascending
    /// pts. A maxPts of 0 means unbounded.
    /// </summary>
    public ValueTask<IReadOnlyCollection<TLSavedMessage>> GetMessagesByPtsAsync(long channelId,
        int minPts, int maxPts = 0);
    /// <summary>
    /// Persists a non-message channel PTS update for difference replay. The update
    /// remains ordinary TL on disk; <paramref name="pts"/> is only its storage key.
    /// </summary>
    public bool PutUpdate(long channelId, int pts, TLUpdate update);
    public ValueTask<IReadOnlyCollection<TLUpdate>> GetUpdatesByPtsAsync(long channelId,
        int minPts, int maxPts = 0);
    public ValueTask<bool> DeleteMessageAsync(long channelId, int messageId);
    public bool DeleteMessages(long channelId);
    public bool PutReadState(TLChannelReadState readState);
    public ValueTask<TLChannelReadState?> GetReadStateAsync(long userId, long channelId);
    public bool DeleteReadState(long userId, long channelId);
}
