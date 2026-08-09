// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public class MessageRepository : IMessageRepository
{
    private readonly IKVStore _store;
    private readonly IUpdatesStateRepository _updatesState;
    public MessageRepository(IKVStore store, IUpdatesStateRepository updatesState)
    {
        _store = store;
        _updatesState = updatesState;
        store.SetSchema(new TableDefinition("ferrite", "messages",
            new KeyDefinition("pk",
                new DataColumn { Name = "user_id", Type = DataType.Long },
                new DataColumn { Name = "peer_type", Type = DataType.Int },
                new DataColumn { Name = "peer_id", Type = DataType.Long },
                new DataColumn { Name = "outgoing", Type = DataType.Bool },
                new DataColumn { Name = "message_id", Type = DataType.Int },
                new DataColumn { Name = "pts", Type = DataType.Int },
                new DataColumn { Name = "date", Type = DataType.Long }),
            new KeyDefinition("by_id",
                new DataColumn { Name = "user_id", Type = DataType.Long },
                new DataColumn { Name = "message_id", Type = DataType.Int })));
    }
    public bool PutMessage(long userId, TLMessage message, int pts)
    {
        var metadata = GetMessageMetadata(message);
        
        using TLSavedMessage savedMessage = SavedMessage.Builder()
            .Pts(pts)
            .OriginalMessage(message.AsSpan())
            .Build();
        _store.Put(savedMessage.AsSpan().ToArray(), userId, metadata.PeerType, metadata.PeerId,
            metadata.Outgoing, MessageIds.GetId(message), pts, metadata.Date);
        _updatesState.PutPts(userId, pts);
        return true;
    }

    private static (int PeerType, long PeerId, bool Outgoing, long Date) GetMessageMetadata(TLMessage message)
    {
        return message.Type switch
        {
            TLMessage.MessageType.Message => GetMessageMetadata(message.AsMessage()),
            TLMessage.MessageType.MessageService => GetMessageMetadata(message.AsMessageService()),
            _ => (0, 0, false, DateTimeOffset.Now.ToUnixTimeSeconds())
        };
    }

    private static (int PeerType, long PeerId, bool Outgoing, long Date) GetMessageMetadata(Message message)
    {
        var peer = message.Get_PeerIdView();
        if (peer.Type is TLPeer.PeerType.PeerChat or TLPeer.PeerType.PeerChannel)
        {
            return ((int)peer.Type, GetPeerId(peer), message.OutProperty, message.Date);
        }

        if (message.OutProperty)
        {
            return ((int)peer.Type, GetPeerId(peer), true, message.Date);
        }

        var from = message.Get_FromIdView();
        return ((int)from.Type, GetPeerId(from), false, message.Date);
    }

    private static (int PeerType, long PeerId, bool Outgoing, long Date) GetMessageMetadata(MessageService message)
    {
        var peer = message.Get_PeerIdView();
        if (peer.Type is TLPeer.PeerType.PeerChat or TLPeer.PeerType.PeerChannel)
        {
            return ((int)peer.Type, GetPeerId(peer), message.OutProperty, message.Date);
        }

        if (message.OutProperty)
        {
            return ((int)peer.Type, GetPeerId(peer), true, message.Date);
        }

        var from = message.Get_FromIdView();
        return ((int)from.Type, GetPeerId(from), false, message.Date);
    }

    private static long GetPeerId(PeerView peer) => peer.Type switch
    {
        TLPeer.PeerType.PeerUser => peer.AsPeerUser().UserId,
        TLPeer.PeerType.PeerChat => peer.AsPeerChat().ChatId,
        TLPeer.PeerType.PeerChannel => peer.AsPeerChannel().ChannelId,
        _ => 0
    };

    public IReadOnlyCollection<TLSavedMessage> GetMessages(long userId, TLInputPeer? peerId = null)
    { 
        List<TLSavedMessage> messages = new();
        if (peerId != null)
        {
            List<object> parameters = new List<object>();
            parameters.Add(userId);
            parameters.Add((int)peerId.Value.Type);
            parameters.Add(GetPeerId(peerId.Value));
            
            var results = _store.Iterate(parameters.ToArray());
            foreach (var val in results)
            {
                messages.Add(new TLSavedMessage(val, 0 ,val.Length));
            }
        }
        else
        {
            var results = _store.Iterate(userId);
            foreach (var val in results)
            {
                messages.Add(new TLSavedMessage(val, 0 ,val.Length));
            }
        }
        messages = messages.OrderByDescending(m => 
            MessageIds.GetId(m.AsSavedMessage().Get_OriginalMessage())).ToList();
        return messages;
    }

    public async ValueTask<IReadOnlyCollection<TLSavedMessage>> GetMessagesAsync(long userId, TLInputPeer? peerId = null)
    {
        List<TLSavedMessage> messages = new();
        if (peerId != null)
        {
            List<object> parameters = new List<object>();
            parameters.Add(userId);
            parameters.Add((int)peerId.Value.Type);
            parameters.Add(GetPeerId(peerId.Value));
            var results = _store.IterateAsync(parameters.ToArray());
            await foreach (var val in results)
            {
                messages.Add(new TLSavedMessage(val, 0 ,val.Length));
            }
        }
        else
        {
            var results = _store.IterateAsync(userId);
            await foreach (var val in results)
            {
                try
                {
                    messages.Add(new TLSavedMessage(val, 0 ,val.Length));
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
        }
        messages = messages.OrderByDescending(m => 
            MessageIds.GetId(m.AsSavedMessage().Get_OriginalMessage())).ToList();
        return messages;
    }

    public IReadOnlyCollection<TLSavedMessage> GetMessages(long userId, int pts, int maxPts, DateTimeOffset date)
    {
        List<TLSavedMessage> messages = new();
        var results = _store.Iterate(userId);
        foreach (var val in results)
        {
            var message = new TLSavedMessage(val, 0, val.Length);
            var messagePts = message.AsSavedMessage().Pts;
            if (messagePts >= pts && messagePts <= maxPts &&
                GetMessageDate(message.AsSavedMessage().Get_OriginalMessage()) <= date.ToUnixTimeSeconds())
            {
                messages.Add(message);
            }
        }
        messages = messages.OrderByDescending(m => 
            MessageIds.GetId(m.AsSavedMessage().Get_OriginalMessage())).ToList();
        return messages;
    }

    public async ValueTask<IReadOnlyCollection<TLSavedMessage>> GetMessagesAsync(long userId, int pts, int maxPts, DateTimeOffset date)
    {
        List<TLSavedMessage> messages = new();
        var results = _store.IterateAsync(userId);
        await foreach (var val in results)
        {
            var message = new TLSavedMessage(val, 0, val.Length);
            var messagePts = message.AsSavedMessage().Pts;
            if (messagePts >= pts && messagePts <= maxPts && 
                GetMessageDate(message.AsSavedMessage().Get_OriginalMessage()) <= date.ToUnixTimeSeconds())
            {
                messages.Add(message);
            }
        }
        messages = messages.OrderByDescending(m => 
            MessageIds.GetId(m.AsSavedMessage().Get_OriginalMessage())).ToList();
        return messages;
    }

    private static int GetMessageDate(TLMessage message) => message.Type switch
    {
        TLMessage.MessageType.Message => message.AsMessage().Date,
        TLMessage.MessageType.MessageService => message.AsMessageService().Date,
        _ => 0
    };

    public TLSavedMessage? GetMessage(long userId, int messageId)
    {
        var data = _store.GetBySecondaryIndex("by_id", userId, messageId);
        if (data == null)
        {
            return null;
        }
        return new TLSavedMessage(data, 0, data.Length);
    }

    public async ValueTask<TLSavedMessage?> GetMessageAsync(long userId, int messageId)
    {
        var data = await _store.GetBySecondaryIndexAsync("by_id", userId, messageId);
        if (data == null)
        {
            return null;
        }
        return new TLSavedMessage(data, 0, data.Length);
    }

    public bool DeleteMessage(long userId, int id)
    {
        return _store.DeleteBySecondaryIndex("by_id", userId, id);
    }

    public async ValueTask<bool> DeleteMessageAsync(long userId, int id)
    {
        return await _store.DeleteBySecondaryIndexAsync("by_id", userId, id);
    }
    
    private static long GetPeerId(TLInputPeer p) => p.Type switch
    {
        TLInputPeer.InputPeerType.InputPeerChat => p.AsInputPeerChat().ChatId,
        TLInputPeer.InputPeerType.InputPeerUser => p.AsInputPeerUser().UserId,
        TLInputPeer.InputPeerType.InputPeerChannel => p.AsInputPeerChannel().ChannelId,
        TLInputPeer.InputPeerType.InputPeerUserFromMessage => p.AsInputPeerUserFromMessage().UserId,
        TLInputPeer.InputPeerType.InputPeerChannelFromMessage => p.AsInputPeerChannelFromMessage().ChannelId,
        _ => 0
    };
}
