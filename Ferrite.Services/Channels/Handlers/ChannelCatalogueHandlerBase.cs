// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.Services.Channels;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

public abstract class ChannelCatalogueHandlerBase : ChannelsHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChannelMessagesRepository _channelMessagesRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;

    protected ChannelCatalogueHandlerBase(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,
        ICounterFactory counterFactory, IdAllocators ids,
        IUpdatesContextFactory updatesContextFactory, IUpdatesService updates,
        ISearchEngine search, IUploadService upload,
        IPhotoProcessingService photos, ILogger log, ChatRowStore chatRows,
        UpdateFanout fanout)
        : base(unitOfWork, channelMessagesRepository, authorizationRepository, channelAdminLogRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, counterFactory, ids, updatesContextFactory, updates,
            search, upload, photos, log, chatRows, fanout)
    {
        _authorizationRepository = authorizationRepository;
        _channelMessagesRepository = channelMessagesRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;

    }

    protected readonly record struct ChannelMembership(long ChannelId, int Role,
        bool Megagroup, bool Broadcast, bool Gigagroup, bool HasActiveUsername,
        bool Forum, int Date);

    protected static Ferrite.TL.baseLayer.messages.TLChats ErrorChats(
        ReadOnlySpan<byte> message) =>
        (Ferrite.TL.baseLayer.messages.TLChats)RpcErrorGenerator.GenerateError(400,
            message);

    protected async Task<long?> ResolveCallerAsync(long authKeyId)
    {
        using TLAuthInfo? auth = await _authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        return auth == null ? null : auth.Value.AsAuthInfo().UserId;
    }

    protected async Task<List<ChannelMembership>> ReadMembershipAsync(long userId)
    {
        var participants = await _chatParticipantsRepository
            .GetParticipantsByUserAsync(userId);
        var roles = new Dictionary<long, int>();
        foreach (TLChatParticipantInfo participant in participants)
        {
            using TLChatParticipantInfo owned = participant;
            var info = owned.AsChatParticipantInfo();
            roles[info.ChatId] = info.Role;
        }

        var result = new List<ChannelMembership>(roles.Count);
        foreach (long chatId in roles.Keys.Order())
        {
            ChannelMembership? membership = await ReadChannelAsync(chatId,
                roles[chatId]);
            if (membership is { } value)
            {
                result.Add(value);
            }
        }

        return result;
    }

    protected async Task<ChannelMembership?> ReadChannelAsync(long channelId, int role)
    {
        using TLChat? chat = await _chatRepository.GetChatAsync(channelId);
        if (chat is not { Type: TLChat.ChatType.Channel })
        {
            return null;
        }

        var channel = chat.Value.AsChannel();
        return new ChannelMembership(channelId, role, channel.Megagroup,
            channel.Broadcast, channel.Gigagroup,
            ChannelUsernames.HasActive(ChannelUsernames.Read(channel)),
            channel.Forum, channel.Date);
    }

    protected static bool IsActiveRole(int role) =>
        role != (int)ChatParticipantRole.Banned &&
        role != (int)ChatParticipantRole.Left;

    protected async Task<Ferrite.TL.baseLayer.messages.TLChats> BuildChatsAsync(
        long viewerUserId, IReadOnlyList<long> channelIds)
    {
        var rows = new List<byte[]>(channelIds.Count);
        foreach (long channelId in channelIds)
        {
            using TLChat? chat = await _chatRepository
                .GetChatAsync(channelId);
            if (chat is not { Type: TLChat.ChatType.Channel })
            {
                continue;
            }

            rows.Add(await ChannelRows.ForViewerAsync(
                _chatParticipantsRepository, viewerUserId, channelId,
                chat.Value.AsSpan().ToArray()));
        }

        var chatVector = new Vector();
        foreach (byte[] row in rows)
        {
            chatVector.AppendTLObject(row);
        }

        return Ferrite.TL.baseLayer.messages.Chats.Builder()
            .ChatsProperty(chatVector)
            .Build();
    }

    protected async Task<int> ReadLastActivityDateAsync(long channelId,
        int createdDate)
    {
        var stored = await _channelMessagesRepository
            .GetMessagesAsync(channelId);
        int latest = 0;
        foreach (TLSavedMessage value in stored)
        {
            using TLSavedMessage savedMessage = value;
            TLMessage message = savedMessage.AsSavedMessage().Get_OriginalMessage();
            if (MessageStore.TryReadStoredMessageInfo(message,
                    out StoredMessageInfo info))
            {
                latest = Math.Max(latest, info.Date);
            }
        }

        return latest > 0 ? latest : createdDate;
    }
}
