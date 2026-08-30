// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using System.Text.RegularExpressions;
using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.channels;
using Ferrite.TL.baseLayer.dto;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

public sealed class GetMessagesHandler : ChannelsHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChannelMessagesRepository _channelMessagesRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;

    public GetMessagesHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository, ICounterFactory counterFactory,
        IdAllocators ids, IUpdatesContextFactory updatesContextFactory,
        IUpdatesService updates, ISearchEngine search, IUploadService upload,
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

    [TLFunction(Constructors.layer51_ChannelsGetMessages)]
    public async Task<Ferrite.TL.baseLayer.messages.TLMessages> HandleLayer51(
        long authKeyId, TLBytes q)
    {
        using var current = ToCurrentGetMessagesRequest(q);
        return await Handle(authKeyId, current);
    }

    private static TLBytes ToCurrentGetMessagesRequest(TLBytes q)
    {
        var sent = new TL.layer51.channels.ChannelsGetMessages(q.AsSpan());
        var currentIds = new Vector();
        VectorOfInt sentIds = sent.Id;
        for (int i = 0; i < sentIds.Count; i++)
        {
            using var message = InputMessageID.Builder().Id(sentIds[i]).Build();
            currentIds.AppendTLObject(message.ToReadOnlySpan());
        }

        using var current = ChannelsGetMessages.Builder()
            .Channel(sent.Channel)
            .Id(currentIds)
            .Build();
        return current.TLBytes!.Value;
    }

    [TLFunction(Constructors.baseLayer_ChannelsGetMessages)]
    public async Task<Ferrite.TL.baseLayer.messages.TLMessages> Handle(long authKeyId, TLBytes q)
    {
        var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
        if (auth == null)
        {
            return (Ferrite.TL.baseLayer.messages.TLMessages)RpcErrorGenerator
                .GenerateError(400, "AUTH_KEY_INVALID"u8);
        }

        long currentUserId = auth.Value.AsAuthInfo().UserId;

        var requestedIds = new List<int>();
        var request = (ChannelsGetMessages)q;
        long? channelId = ResolveInputChannelId(request.Get_ChannelView());
        var idVector = request.Id;
        int idCount = idVector.Count;
        for (int i = 0; i < idCount; i++)
        {
            InputMessageView inputMessage = idVector.ReadTLObject();
            if (inputMessage.Is(out InputMessageID messageById))
            {
                requestedIds.Add(messageById.Id);
            }
        }

        if (channelId is not > 0)
        {
            return (Ferrite.TL.baseLayer.messages.TLMessages)RpcErrorGenerator
                .GenerateError(400, "CHANNEL_INVALID"u8);
        }

        using var channel = await _chatRepository.GetChatAsync(channelId.Value);
        if (channel == null || channel.Value.Type != TLChat.ChatType.Channel)
        {
            return (Ferrite.TL.baseLayer.messages.TLMessages)RpcErrorGenerator
                .GenerateError(400, "CHANNEL_INVALID"u8);
        }

        var participant = await _chatParticipantsRepository
            .GetParticipantAsync(channelId.Value, currentUserId);
        bool member = participant != null && IsActiveParticipant(participant.Value);
        participant?.Dispose();
        if (!member)
        {
            return (Ferrite.TL.baseLayer.messages.TLMessages)RpcErrorGenerator
                .GenerateError(400, "CHANNEL_PRIVATE"u8);
        }

        var channelBox = new ChannelMessageBox(_counterFactory, channelId.Value);
        int channelPts = await channelBox.Pts();

        var messageBytes = new List<byte[]>();
        var senderIds = new HashSet<long>();
        var relatedChatIds = new HashSet<long> { channelId.Value };
        foreach (int messageId in requestedIds)
        {
            var saved = await _channelMessagesRepository
                .GetMessageAsync(channelId.Value, messageId);
            if (saved == null)
            {
                continue;
            }

            using var savedMessage = saved.Value;
            var message = savedMessage.AsSavedMessage().Get_OriginalMessage();
            byte[] bytes = message.AsSpan().ToArray();
            messageBytes.Add(bytes);
            long senderId = ResolveMessageSenderId(bytes);
            if (senderId > 0)
            {
                senderIds.Add(senderId);
            }
            long actionChatId = ResolveMigrationActionChatId(bytes);
            if (actionChatId > 0)
            {
                relatedChatIds.Add(actionChatId);
            }
        }

        var relatedChatBytes = new List<byte[]>();
        foreach (long relatedChatId in relatedChatIds)
        {
            if (relatedChatId == channelId.Value)
            {
                relatedChatBytes.Add(channel.Value.AsSpan().ToArray());
                continue;
            }
            using var relatedChat = await _chatRepository
                .GetChatAsync(relatedChatId);
            if (relatedChat != null)
            {
                relatedChatBytes.Add(relatedChat.Value.AsSpan().ToArray());
            }
        }

        var messageVector = new Vector();
        foreach (byte[] bytes in messageBytes)
        {
            messageVector.AppendTLObject(bytes);
        }
        var userVector = new Vector();
        AppendUsers(currentUserId, ref userVector, senderIds);
        var chatVector = new Vector();
        foreach (byte[] relatedChatBytesItem in relatedChatBytes)
        {
            chatVector.AppendTLObject(relatedChatBytesItem);
        }

        _log.Debug($"📣 channels.GetMessages channel:{channelId.Value} requested:{requestedIds.Count} " +
                   $"-> {messageBytes.Count} pts:{channelPts}");

        return Ferrite.TL.baseLayer.messages.ChannelMessages.Builder()
            .Pts(channelPts)
            .Count(messageBytes.Count)
            .Messages(messageVector)
            .Topics(new Vector())
            .Chats(chatVector)
            .Users(userVector)
            .Build();
    }
}
