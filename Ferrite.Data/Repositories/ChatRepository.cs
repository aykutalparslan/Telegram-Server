// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public class ChatRepository : IChatRepository
{
    private readonly IKVStore _store;
    private readonly IKVStore _storeFullInfo;
    private readonly IKVStore _storeUsernames;
    public ChatRepository(IKVStore store, IKVStore storeFullInfo, IKVStore storeUsernames)
    {
        _store = store;
        store.SetSchema(new TableDefinition("ferrite", "chats",
            new KeyDefinition("pk",
                new DataColumn { Name = "chat_id", Type = DataType.Long })));
        _storeFullInfo = storeFullInfo;
        storeFullInfo.SetSchema(new TableDefinition("ferrite", "chat_full",
            new KeyDefinition("pk",
                new DataColumn { Name = "chat_id", Type = DataType.Long })));
        _storeUsernames = storeUsernames;
        storeUsernames.SetSchema(new TableDefinition("ferrite", "chat_usernames_tl1",
            new KeyDefinition("pk",
                new DataColumn { Name = "username", Type = DataType.String })));
    }
    public bool PutChat(TLChat chat)
    {
        long chatId = chat.Type switch
        {
            TLChat.ChatType.Chat => chat.AsChat().Id,
            TLChat.ChatType.ChatForbidden => chat.AsChatForbidden().Id,
            TLChat.ChatType.Channel => chat.AsChannel().Id,
            _ => 0
        };
        return _store.Put(chat.AsSpan().ToArray(), chatId);
    }

    public async ValueTask<TLChat?> GetChatAsync(long chatId)
    {
        var chatBytes = await _store.GetAsync(chatId);
        if (chatBytes is { Length: > 0 })
        {
            return new TLChat(chatBytes, 0, chatBytes.Length);
        }

        return null;
    }

    public bool DeleteChat(long chatId)
    {
        bool deleted = _store.Delete(chatId);
        return _storeFullInfo.Delete(chatId) && deleted;
    }

    public bool PutFullInfo(TLChatFullInfo fullInfo)
    {
        return _storeFullInfo.Put(fullInfo.AsSpan().ToArray(),
            fullInfo.AsChatFullInfo().ChatId);
    }

    public async ValueTask<TLChatFullInfo?> GetFullInfoAsync(long chatId)
    {
        var fullInfoBytes = await _storeFullInfo.GetAsync(chatId);
        if (fullInfoBytes is { Length: > 0 })
        {
            return new TLChatFullInfo(fullInfoBytes, 0, fullInfoBytes.Length);
        }

        return null;
    }

    public bool DeleteFullInfo(long chatId)
    {
        return _storeFullInfo.Delete(chatId);
    }

    public bool PutUsername(string username, long chatId)
    {
        using var row = ChatUsernameReference.Builder().ChatId(chatId).Build();
        return _storeUsernames.Put(row.ToReadOnlySpan().ToArray(), username);
    }

    public long? GetChatIdByUsername(string username)
    {
        var chatIdBytes = _storeUsernames.Get(username);
        if (chatIdBytes is { Length: > 0 })
        {
            var value = new TLBytes(chatIdBytes, 0, chatIdBytes.Length);
            if (value.Constructor != Constructors.baseLayer_ChatUsernameReference)
                throw new InvalidDataException("Chat username codec/version mismatch.");
            return ((TLChatUsernameReference)value).AsChatUsernameReference().ChatId;
        }

        return null;
    }

    public bool DeleteUsername(string username)
    {
        return _storeUsernames.Delete(username);
    }
}
