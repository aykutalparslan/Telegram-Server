// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public class MessageReactionsRepository : IMessageReactionsRepository
{
    private readonly IKVStore _reactions;
    private readonly IKVStore _copies;
    private readonly IKVStore _settings;

    public MessageReactionsRepository(IKVStore reactions, IKVStore copies, IKVStore settings)
    {
        _reactions = reactions;
        reactions.SetSchema(new TableDefinition("ferrite", "message_reactions",
            new KeyDefinition("pk",
                new DataColumn { Name = "box_type", Type = DataType.Int },
                new DataColumn { Name = "box_id", Type = DataType.Long },
                new DataColumn { Name = "message_id", Type = DataType.Int },
                new DataColumn { Name = "user_id", Type = DataType.Long }),
            // Secondary index rows are unique per index key, so the per-user index
            // carries message_id to keep one entry per reacted message.
            new KeyDefinition("by_user",
                new DataColumn { Name = "box_type", Type = DataType.Int },
                new DataColumn { Name = "box_id", Type = DataType.Long },
                new DataColumn { Name = "user_id", Type = DataType.Long },
                new DataColumn { Name = "message_id", Type = DataType.Int })));
        _copies = copies;
        copies.SetSchema(new TableDefinition("ferrite", "message_copies",
            new KeyDefinition("pk",
                new DataColumn { Name = "logical_id", Type = DataType.Long },
                new DataColumn { Name = "user_id", Type = DataType.Long },
                new DataColumn { Name = "message_id", Type = DataType.Int }),
            new KeyDefinition("by_owner",
                new DataColumn { Name = "user_id", Type = DataType.Long },
                new DataColumn { Name = "message_id", Type = DataType.Int })));
        _settings = settings;
        settings.SetSchema(new TableDefinition("ferrite", "reaction_settings",
            new KeyDefinition("pk",
                new DataColumn { Name = "user_id", Type = DataType.Long })));
    }

    public bool PutReaction(TLMessageReactionInfo reaction)
    {
        var info = reaction.AsMessageReactionInfo();
        return _reactions.Put(reaction.AsSpan().ToArray(), info.BoxType, info.BoxId,
            info.MessageId, info.UserId);
    }

    public async ValueTask<TLMessageReactionInfo?> GetReactionAsync(int boxType, long boxId,
        int messageId, long userId)
    {
        byte[]? bytes = await _reactions.GetAsync(boxType, boxId, messageId, userId);
        return bytes is { Length: > 0 }
            ? new TLMessageReactionInfo(bytes, 0, bytes.Length)
            : null;
    }

    public async ValueTask<IReadOnlyCollection<TLMessageReactionInfo>> GetReactionsAsync(
        int boxType, long boxId, int messageId)
    {
        List<TLMessageReactionInfo> reactions = new();
        await foreach (byte[] bytes in _reactions.IterateAsync(boxType, boxId, messageId))
        {
            reactions.Add(new TLMessageReactionInfo(bytes, 0, bytes.Length));
        }
        return reactions;
    }

    public async ValueTask<IReadOnlyCollection<TLMessageReactionInfo>> GetBoxReactionsAsync(
        int boxType, long boxId)
    {
        List<TLMessageReactionInfo> reactions = new();
        await foreach (byte[] bytes in _reactions.IterateAsync(boxType, boxId))
        {
            reactions.Add(new TLMessageReactionInfo(bytes, 0, bytes.Length));
        }
        return reactions;
    }

    public async ValueTask<IReadOnlyCollection<TLMessageReactionInfo>> GetUserReactionsAsync(
        int boxType, long boxId, long userId)
    {
        List<TLMessageReactionInfo> reactions = new();
        await foreach (byte[] bytes in _reactions.IterateBySecondaryIndexAsync("by_user",
            boxType, boxId, userId))
        {
            reactions.Add(new TLMessageReactionInfo(bytes, 0, bytes.Length));
        }
        return reactions;
    }

    public bool DeleteReaction(int boxType, long boxId, int messageId, long userId) =>
        _reactions.Delete(boxType, boxId, messageId, userId);

    public bool DeleteReactions(int boxType, long boxId, int messageId) =>
        _reactions.Delete(boxType, boxId, messageId);

    public bool DeleteBoxReactions(int boxType, long boxId) =>
        _reactions.Delete(boxType, boxId);

    public bool PutMessageCopy(TLMessageCopyInfo copy)
    {
        var info = copy.AsMessageCopyInfo();
        return _copies.Put(copy.AsSpan().ToArray(), info.LogicalId, info.UserId,
            info.MessageId);
    }

    public async ValueTask<IReadOnlyCollection<TLMessageCopyInfo>> GetMessageCopiesAsync(
        long logicalId)
    {
        List<TLMessageCopyInfo> copies = new();
        await foreach (byte[] bytes in _copies.IterateAsync(logicalId))
        {
            copies.Add(new TLMessageCopyInfo(bytes, 0, bytes.Length));
        }
        return copies;
    }

    public async ValueTask<TLMessageCopyInfo?> GetCopyByOwnerMessageAsync(long userId,
        int messageId)
    {
        byte[]? bytes = await _copies.GetBySecondaryIndexAsync("by_owner", userId, messageId);
        return bytes is { Length: > 0 }
            ? new TLMessageCopyInfo(bytes, 0, bytes.Length)
            : null;
    }

    public bool DeleteMessageCopies(long logicalId) => _copies.Delete(logicalId);

    public bool PutReactionSettings(TLReactionSettingsInfo settings)
    {
        var info = settings.AsReactionSettingsInfo();
        return _settings.Put(settings.AsSpan().ToArray(), info.UserId);
    }

    public async ValueTask<TLReactionSettingsInfo?> GetReactionSettingsAsync(long userId)
    {
        byte[]? bytes = await _settings.GetAsync(userId);
        return bytes is { Length: > 0 }
            ? new TLReactionSettingsInfo(bytes, 0, bytes.Length)
            : null;
    }
}
