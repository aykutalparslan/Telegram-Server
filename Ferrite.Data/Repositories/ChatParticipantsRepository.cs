// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public class ChatParticipantsRepository : IChatParticipantsRepository
{
    private readonly IKVStore _store;
    public ChatParticipantsRepository(IKVStore store)
    {
        _store = store;
        store.SetSchema(new TableDefinition("ferrite", "chat_participants",
            new KeyDefinition("pk",
                new DataColumn { Name = "chat_id", Type = DataType.Long },
                new DataColumn { Name = "user_id", Type = DataType.Long }),
            new KeyDefinition("by_user",
                new DataColumn { Name = "user_id", Type = DataType.Long },
                new DataColumn { Name = "chat_id", Type = DataType.Long })));
    }

    public bool PutParticipant(TLChatParticipantInfo participant)
    {
        long chatId = participant.AsChatParticipantInfo().ChatId;
        long userId = participant.AsChatParticipantInfo().UserId;
        return _store.Put(participant.AsSpan().ToArray(), chatId, userId);
    }

    public async ValueTask<TLChatParticipantInfo?> GetParticipantAsync(long chatId, long userId)
    {
        var participantBytes = await _store.GetAsync(chatId, userId);
        if (participantBytes is { Length: > 0 })
        {
            return new TLChatParticipantInfo(participantBytes, 0, participantBytes.Length);
        }

        return null;
    }

    public async ValueTask<IReadOnlyCollection<TLChatParticipantInfo>> GetParticipantsAsync(long chatId)
    {
        List<TLChatParticipantInfo> participants = new();
        await foreach (var participantBytes in _store.IterateAsync(chatId))
        {
            participants.Add(new TLChatParticipantInfo(participantBytes, 0, participantBytes.Length));
        }

        return participants;
    }

    public async ValueTask<IReadOnlyCollection<TLChatParticipantInfo>> GetParticipantsByUserAsync(long userId)
    {
        List<TLChatParticipantInfo> participants = new();
        await foreach (var participantBytes in _store.IterateBySecondaryIndexAsync("by_user", userId))
        {
            participants.Add(new TLChatParticipantInfo(participantBytes, 0, participantBytes.Length));
        }

        return participants;
    }

    public bool DeleteParticipant(long chatId, long userId)
    {
        return _store.Delete(chatId, userId);
    }

    public bool DeleteParticipants(long chatId)
    {
        return _store.Delete(chatId);
    }
}
