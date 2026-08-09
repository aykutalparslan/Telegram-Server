// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public class ChannelMessagesRepository : IChannelMessagesRepository
{
    private readonly IKVStore _store;
    private readonly IKVStore _storeReadStates;
    private readonly IKVStore _storeUpdates;
    public ChannelMessagesRepository(IKVStore store, IKVStore storeReadStates,
        IKVStore storeUpdates)
    {
        _store = store;
        store.SetSchema(new TableDefinition("ferrite", "channel_messages",
            new KeyDefinition("pk",
                new DataColumn { Name = "channel_id", Type = DataType.Long },
                new DataColumn { Name = "message_id", Type = DataType.Int })));
        _storeReadStates = storeReadStates;
        storeReadStates.SetSchema(new TableDefinition("ferrite", "channel_read_states",
            new KeyDefinition("pk",
                new DataColumn { Name = "user_id", Type = DataType.Long },
                new DataColumn { Name = "channel_id", Type = DataType.Long })));
        _storeUpdates = storeUpdates;
        storeUpdates.SetSchema(new TableDefinition("ferrite", "channel_updates",
            new KeyDefinition("pk",
                new DataColumn { Name = "channel_id", Type = DataType.Long },
                new DataColumn { Name = "pts", Type = DataType.Int })));
    }

    public bool PutMessage(long channelId, TLMessage message, int pts)
    {
        using TLSavedMessage savedMessage = SavedMessage.Builder()
            .Pts(pts)
            .OriginalMessage(message.AsSpan())
            .Build();
        return _store.Put(savedMessage.AsSpan().ToArray(), channelId,
            MessageIds.GetId(message));
    }

    public async ValueTask<TLSavedMessage?> GetMessageAsync(long channelId, int messageId)
    {
        var messageBytes = await _store.GetAsync(channelId, messageId);
        if (messageBytes is { Length: > 0 })
        {
            return new TLSavedMessage(messageBytes, 0, messageBytes.Length);
        }

        return null;
    }

    public async ValueTask<IReadOnlyCollection<TLSavedMessage>> GetMessagesAsync(long channelId,
        int minId = 0, int maxId = 0)
    {
        List<TLSavedMessage> messages = new();
        await foreach (var messageBytes in _store.IterateAsync(channelId))
        {
            var message = new TLSavedMessage(messageBytes, 0, messageBytes.Length);
            int messageId = MessageIds.GetId(message.AsSavedMessage().Get_OriginalMessage());
            if ((minId == 0 || messageId >= minId) && (maxId == 0 || messageId <= maxId))
            {
                messages.Add(message);
            }
        }

        return messages.OrderByDescending(m =>
            MessageIds.GetId(m.AsSavedMessage().Get_OriginalMessage())).ToList();
    }

    public async ValueTask<IReadOnlyCollection<TLSavedMessage>> GetMessagesByPtsAsync(long channelId,
        int minPts, int maxPts = 0)
    {
        List<TLSavedMessage> messages = new();
        await foreach (var messageBytes in _store.IterateAsync(channelId))
        {
            var message = new TLSavedMessage(messageBytes, 0, messageBytes.Length);
            int messagePts = message.AsSavedMessage().Pts;
            if (messagePts >= minPts && (maxPts == 0 || messagePts <= maxPts))
            {
                messages.Add(message);
            }
        }

        return messages.OrderBy(m => m.AsSavedMessage().Pts).ToList();
    }

    public bool PutUpdate(long channelId, int pts, TLUpdate update) =>
        _storeUpdates.Put(update.AsSpan().ToArray(), channelId, pts);

    public async ValueTask<IReadOnlyCollection<TLUpdate>> GetUpdatesByPtsAsync(long channelId,
        int minPts, int maxPts = 0)
    {
        var updates = new List<(int Pts, TLUpdate Update)>();
        await foreach (byte[] bytes in _storeUpdates.IterateAsync(channelId))
        {
            var update = new TLUpdate(bytes, 0, bytes.Length);
            int pts = GetPts(update);
            if (pts >= minPts && (maxPts == 0 || pts <= maxPts))
            {
                updates.Add((pts, update));
            }
            else
            {
                update.Dispose();
            }
        }
        return updates.OrderBy(item => item.Pts).Select(item => item.Update).ToList();
    }

    private static int GetPts(TLUpdate update) => update.Constructor switch
    {
        Constructors.baseLayer_UpdateDeleteChannelMessages =>
            update.AsUpdateDeleteChannelMessages().Pts,
        Constructors.baseLayer_UpdateEditChannelMessage =>
            update.AsUpdateEditChannelMessage().Pts,
        Constructors.baseLayer_UpdatePinnedChannelMessages =>
            update.AsUpdatePinnedChannelMessages().Pts,
        _ => 0
    };

    public async ValueTask<bool> DeleteMessageAsync(long channelId, int messageId)
    {
        return await _store.DeleteAsync(channelId, messageId);
    }

    public bool DeleteMessages(long channelId)
    {
        bool messages = _store.Delete(channelId);
        bool updates = _storeUpdates.Delete(channelId);
        return messages && updates;
    }

    public bool PutReadState(TLChannelReadState readState)
    {
        long userId = readState.AsChannelReadState().UserId;
        long channelId = readState.AsChannelReadState().ChannelId;
        return _storeReadStates.Put(readState.AsSpan().ToArray(), userId, channelId);
    }

    public async ValueTask<TLChannelReadState?> GetReadStateAsync(long userId, long channelId)
    {
        var readStateBytes = await _storeReadStates.GetAsync(userId, channelId);
        if (readStateBytes is { Length: > 0 })
        {
            return new TLChannelReadState(readStateBytes, 0, readStateBytes.Length);
        }

        return null;
    }

    public bool DeleteReadState(long userId, long channelId)
    {
        return _storeReadStates.Delete(userId, channelId);
    }
}
