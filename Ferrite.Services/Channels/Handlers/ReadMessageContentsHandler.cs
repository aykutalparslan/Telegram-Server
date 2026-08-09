// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.channels;
using Ferrite.TL.baseLayer.dto;
using System.Text;

namespace Ferrite.Services.Handlers.Channels;

/// <summary>
/// A channel post exists once in the shared box, so marking its content read is
/// recorded per viewer. The stored message keeps its mention/media-unread flags
/// for everyone who has not read it, and only the caller's sessions are told.
/// </summary>
public sealed class ReadMessageContentsHandler
{
    private readonly IChatParticipantsRepository _chatParticipantsRepository;

    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChannelContentReadsRepository _channelContentReadsRepository;
    private readonly IChannelMessagesRepository _channelMessagesRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IUserRepository _userRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IUpdatesService _updates;
    private readonly TimeProvider _timeProvider;

    public ReadMessageContentsHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IAuthorizationRepository authorizationRepository, IChannelContentReadsRepository channelContentReadsRepository, IChannelMessagesRepository channelMessagesRepository, IChatRepository chatRepository, IUserRepository userRepository,
        IUpdatesService updates, TimeProvider timeProvider)
    {
        _chatParticipantsRepository = chatParticipantsRepository;

        _authorizationRepository = authorizationRepository;
        _channelContentReadsRepository = channelContentReadsRepository;
        _channelMessagesRepository = channelMessagesRepository;
        _chatRepository = chatRepository;
        _userRepository = userRepository;

        _unitOfWork = unitOfWork;
        _updates = updates;
        _timeProvider = timeProvider;
    }

    [TLFunction(Constructors.baseLayer_ChannelsReadMessageContents)]
    public async Task<TLBool> Handle(long authKeyId, TLBytes q)
    {
        long userId;
        using (TLAuthInfo? auth = await _authorizationRepository
                   .GetAuthorizationAsync(authKeyId))
        {
            if (auth == null || !auth.Value.AsAuthInfo().LoggedIn)
            {
                return Error("AUTH_KEY_INVALID");
            }
            userId = auth.Value.AsAuthInfo().UserId;
        }

        var request = (ChannelsReadMessageContents)q;
        long? channelId = ResolveChannelId(request.Get_ChannelView());
        List<int> requestedIds = ReadMessageIds(request.Id);
        if (channelId is not > 0)
        {
            return Error("CHANNEL_INVALID");
        }

        using (TLChat? channel = await _chatRepository
                   .GetChatAsync(channelId.Value))
        {
            if (channel == null || channel.Value.Type != TLChat.ChatType.Channel)
            {
                return Error("CHANNEL_INVALID");
            }
        }

        using (TLChatParticipantInfo? participant = await _chatParticipantsRepository.GetParticipantAsync(channelId.Value,
                       userId))
        {
            if (participant == null || !IsActive(participant.Value))
            {
                return Error("CHANNEL_PRIVATE");
            }
        }

        int readAt = checked((int)_timeProvider.GetUtcNow().ToUnixTimeSeconds());
        string? username = null;
        using (TLUser? user = _userRepository.GetUser(userId))
        {
            if (user != null && user.Value.AsUser().Username.Length > 0)
            {
                username = Encoding.UTF8.GetString(user.Value.AsUser().Username);
            }
        }
        var newlyRead = new List<int>();
        var seen = new HashSet<int>();
        foreach (int messageId in requestedIds)
        {
            if (messageId <= 0 || !seen.Add(messageId))
            {
                continue;
            }
            if (!await IsUnreadContentAsync(channelId.Value, messageId, userId,
                    username))
            {
                continue;
            }

            using TLChannelContentRead read = ChannelContentRead.Builder()
                .UserId(userId)
                .ChannelId(channelId.Value)
                .MessageId(messageId)
                .ReadAt(readAt)
                .Build();
            if (_channelContentReadsRepository.PutContentRead(read))
            {
                newlyRead.Add(messageId);
            }
        }

        if (newlyRead.Count == 0)
        {
            return BoolTrue.Builder().Build();
        }
        if (!await _unitOfWork.SaveAsync())
        {
            return Error("INTERNAL_SERVER_ERROR");
        }

        // EnqueueUpdate owns the value it is handed and disposes it, so the pooled
        // update is transferred rather than scoped with `using`.
        await _updates.EnqueueUpdate(userId,
            BuildUpdate(channelId.Value, newlyRead));
        return BoolTrue.Builder().Build();
    }

    /// <summary>
    /// Content is readable exactly once per viewer, is never read by its own
    /// author, and only applies to a message the shared box still marks unread.
    /// </summary>
    private async ValueTask<bool> IsUnreadContentAsync(long channelId,
        int messageId, long userId, string? username)
    {
        using TLSavedMessage? saved = await _channelMessagesRepository
            .GetMessageAsync(channelId, messageId);
        if (saved == null)
        {
            return false;
        }

        TLMessage original = saved.Value.AsSavedMessage().Get_OriginalMessage();
        if (original.Type != TLMessage.MessageType.Message)
        {
            return false;
        }
        var message = original.AsMessage();
        if (message.Flags[8] &&
            PeerResolver.TryReadPeer(message.Get_FromIdView(), out var from) &&
            from.Type == TLPeer.PeerType.PeerUser && from.Id == userId)
        {
            return false;
        }
        // The shared channel row deliberately has no viewer-specific `mentioned`
        // bit. Derive it from its canonical entities just as difference replay
        // does; otherwise channels.readMessageContents can never persist a read
        // marker for a named member.
        if (!message.Flags[4] && !message.Flags[5] &&
            !MessageMentions.MentionsUser(message, userId, username))
        {
            return false;
        }

        using TLChannelContentRead? existing = await _channelContentReadsRepository.GetContentReadAsync(userId, channelId,
                messageId);
        return existing == null;
    }

    private static TLUpdate BuildUpdate(long channelId, IReadOnlyList<int> messageIds)
    {
        var ids = new VectorOfInt();
        foreach (int messageId in messageIds)
        {
            ids.Append(messageId);
        }
        return UpdateChannelReadMessagesContents.Builder()
            .ChannelId(channelId)
            .Messages(ids)
            .Build();
    }

    private static List<int> ReadMessageIds(VectorOfInt ids)
    {
        var messageIds = new List<int>(ids.Count);
        for (int i = 0; i < ids.Count; i++)
        {
            messageIds.Add(ids[i]);
        }
        return messageIds;
    }

    private static long? ResolveChannelId(InputChannelView channel)
    {
        if (channel.Is(out InputChannel direct))
        {
            return direct.ChannelId;
        }
        if (channel.Is(out InputChannelFromMessage fromMessage))
        {
            return fromMessage.ChannelId;
        }
        return null;
    }

    private static bool IsActive(TLChatParticipantInfo participant)
    {
        int role = participant.AsChatParticipantInfo().Role;
        return role != (int)ChatParticipantRole.Banned &&
               role != (int)ChatParticipantRole.Left;
    }

    private static TLBool Error(string message) =>
        (TLBool)RpcErrorGenerator.GenerateError(400,
            System.Text.Encoding.UTF8.GetBytes(message));
}
