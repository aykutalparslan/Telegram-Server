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
    public ValueTask<IReadOnlyCollection<TLSavedMessage>> GetMessagesAsync(long channelId,
        int minId = 0, int maxId = 0);
    public ValueTask<IReadOnlyCollection<TLSavedMessage>> GetMessagesByPtsAsync(long channelId,
        int minPts, int maxPts = 0);
    public bool PutUpdate(long channelId, int pts, TLUpdate update);
    public ValueTask<IReadOnlyCollection<TLUpdate>> GetUpdatesByPtsAsync(long channelId,
        int minPts, int maxPts = 0);
    public ValueTask<bool> DeleteMessageAsync(long channelId, int messageId);
    public bool DeleteMessages(long channelId);
    public bool PutReadState(TLChannelReadState readState);
    public ValueTask<TLChannelReadState?> GetReadStateAsync(long userId, long channelId);
    public bool DeleteReadState(long userId, long channelId);
}
