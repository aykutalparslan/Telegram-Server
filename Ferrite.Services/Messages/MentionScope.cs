// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Messages;

public sealed class MentionScope
{
    private readonly IChannelContentReadsRepository _channelContentReadsRepository;

    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;

    private readonly IUserRepository _userRepository;

    private readonly IUnitOfWork _unitOfWork;

    public MentionScope(IUnitOfWork unitOfWork, IChannelContentReadsRepository channelContentReadsRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IUserRepository userRepository)
    {
        _channelContentReadsRepository = channelContentReadsRepository;

        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;

        _userRepository = userRepository;

        _unitOfWork = unitOfWork;
    }

    public static List<MessageSnapshot> SelectUnreadCommonMentions(
        IReadOnlyList<MessageSnapshot> conversation, int topMsgId)
    {
        var mentions = new List<MessageSnapshot>();
        foreach (MessageSnapshot snapshot in conversation)
        {
            if (IsUnreadCommonMention(snapshot.Bytes, snapshot.Id, topMsgId))
            {
                mentions.Add(snapshot);
            }
        }
        return mentions;
    }

    public static bool IsUnreadCommonMention(byte[] messageBytes, int messageId,
        int topMsgId)
    {
        using var stored = new TLMessage(messageBytes, 0, messageBytes.Length);
        if (stored.Type != TLMessage.MessageType.Message)
        {
            return false;
        }
        var message = stored.AsMessage();
        return message.Mentioned && !message.OutProperty &&
               InTopic(messageBytes, messageId, topMsgId);
    }

    public async Task<List<MessageSnapshot>> SelectUnreadChannelMentionsAsync(
        long channelId, long userId, IReadOnlyList<MessageSnapshot> posts,
        int topMsgId)
    {
        string? username = ReadUsername(userId);
        var candidates = new List<MessageSnapshot>();
        foreach (MessageSnapshot snapshot in posts)
        {
            byte[] bytes = snapshot.Bytes;
            using var stored = new TLMessage(bytes, 0, bytes.Length);
            if (stored.Type != TLMessage.MessageType.Message ||
                !InTopic(bytes, snapshot.Id, topMsgId))
            {
                continue;
            }
            var message = stored.AsMessage();
            if (ForumMessages.ResolveStoredMessageSenderId(bytes) != userId &&
                MessageMentions.MentionsUser(message, userId, username))
            {
                candidates.Add(snapshot);
            }
        }

        var unread = new List<MessageSnapshot>();
        foreach (MessageSnapshot candidate in candidates)
        {
            if (!await IsContentReadAsync(userId, channelId, candidate.Id))
            {
                unread.Add(candidate);
            }
        }
        return unread;
    }

    public ValueTask<string?> ValidateChannelAccessAsync(long channelId,
        long userId) =>
        ChannelAccess.ValidateReadAsync(_chatRepository, _chatParticipantsRepository, channelId, userId);

    public async ValueTask<bool> IsContentReadAsync(long userId, long channelId,
        int messageId)
    {
        using TLChannelContentRead? read = await _channelContentReadsRepository.GetContentReadAsync(userId, channelId,
                messageId);
        return read != null;
    }

    private string? ReadUsername(long userId)
    {
        using TLUser? user = _userRepository.GetUser(userId);
        if (user == null)
        {
            return null;
        }
        var body = user.Value.AsUser();
        return body.Flags[3] ? Encoding.UTF8.GetString(body.Username) : null;
    }

    private static bool InTopic(byte[] messageBytes, int messageId, int topMsgId) =>
        topMsgId <= 0 ||
        ForumMessages.ResolveStoredForumTopicId(messageBytes, messageId) == topMsgId;
}
