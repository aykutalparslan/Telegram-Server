// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public class ChatInvitesRepository : IChatInvitesRepository
{
    private readonly IKVStore _store;
    private readonly IKVStore _storeImporters;
    private readonly IKVStore _storePendingImporters;

    public ChatInvitesRepository(IKVStore store, IKVStore storeImporters,
        IKVStore storePendingImporters)
    {
        _store = store;
        store.SetSchema(new TableDefinition("ferrite", "chat_invites",
            new KeyDefinition("pk",
                new DataColumn { Name = "chat_id", Type = DataType.Long },
                new DataColumn { Name = "hash", Type = DataType.String }),
            new KeyDefinition("by_hash",
                new DataColumn { Name = "hash", Type = DataType.String })));
        _storeImporters = storeImporters;
        storeImporters.SetSchema(new TableDefinition("ferrite", "chat_invite_importers",
            new KeyDefinition("pk",
                new DataColumn { Name = "chat_id", Type = DataType.Long },
                new DataColumn { Name = "user_id", Type = DataType.Long })));
        _storePendingImporters = storePendingImporters;
        storePendingImporters.SetSchema(new TableDefinition("ferrite",
            "pending_chat_invite_importers",
            new KeyDefinition("pk",
                new DataColumn { Name = "chat_id", Type = DataType.Long },
                new DataColumn { Name = "user_id", Type = DataType.Long })));
    }

    public bool PutInvite(TLChatInviteInfo invite)
    {
        var info = invite.AsChatInviteInfo();
        return _store.Put(invite.AsSpan().ToArray(), info.ChatId,
            Encoding.UTF8.GetString(info.Hash));
    }

    public async ValueTask<TLChatInviteInfo?> GetInviteAsync(long chatId, string hash)
    {
        var inviteBytes = await _store.GetAsync(chatId, hash);
        if (inviteBytes is { Length: > 0 })
        {
            return new TLChatInviteInfo(inviteBytes, 0, inviteBytes.Length);
        }

        return null;
    }

    public TLChatInviteInfo? GetInviteByHash(string hash)
    {
        var inviteBytes = _store.GetBySecondaryIndex("by_hash", hash);
        if (inviteBytes is { Length: > 0 })
        {
            return new TLChatInviteInfo(inviteBytes, 0, inviteBytes.Length);
        }

        return null;
    }

    public async ValueTask<IReadOnlyCollection<TLChatInviteInfo>> GetInvitesAsync(long chatId)
    {
        List<TLChatInviteInfo> invites = new();
        await foreach (var inviteBytes in _store.IterateAsync(chatId))
        {
            invites.Add(new TLChatInviteInfo(inviteBytes, 0, inviteBytes.Length));
        }

        return invites;
    }

    public bool DeleteInvite(long chatId, string hash)
    {
        return _store.Delete(chatId, hash);
    }

    public bool DeleteInvites(long chatId)
    {
        return _store.Delete(chatId);
    }

    public bool PutImporter(TLChatInviteImporterInfo importer)
    {
        var info = importer.AsChatInviteImporterInfo();
        return _storeImporters.Put(importer.AsSpan().ToArray(), info.ChatId, info.UserId);
    }

    public async ValueTask<IReadOnlyCollection<TLChatInviteImporterInfo>> GetImportersAsync(long chatId)
    {
        List<TLChatInviteImporterInfo> importers = new();
        await foreach (var importerBytes in _storeImporters.IterateAsync(chatId))
        {
            importers.Add(new TLChatInviteImporterInfo(importerBytes, 0, importerBytes.Length));
        }

        return importers;
    }

    public bool DeleteImporters(long chatId)
    {
        return _storeImporters.Delete(chatId);
    }

    public bool PutPendingImporter(TLPendingChatInviteImporter importer)
    {
        var info = importer.AsPendingChatInviteImporter();
        return _storePendingImporters.Put(importer.AsSpan().ToArray(), info.ChatId,
            info.UserId);
    }

    public async ValueTask<TLPendingChatInviteImporter?> GetPendingImporterAsync(
        long chatId, long userId)
    {
        byte[]? bytes = await _storePendingImporters.GetAsync(chatId, userId);
        return bytes is { Length: > 0 }
            ? new TLPendingChatInviteImporter(bytes, 0, bytes.Length)
            : null;
    }

    public async ValueTask<IReadOnlyCollection<TLPendingChatInviteImporter>>
        GetPendingImportersAsync(long chatId)
    {
        var importers = new List<TLPendingChatInviteImporter>();
        await foreach (byte[] bytes in _storePendingImporters.IterateAsync(chatId))
        {
            importers.Add(new TLPendingChatInviteImporter(bytes, 0, bytes.Length));
        }
        return importers;
    }

    public bool DeletePendingImporter(long chatId, long userId) =>
        _storePendingImporters.Delete(chatId, userId);

    public bool DeletePendingImporters(long chatId) =>
        _storePendingImporters.Delete(chatId);
}
