// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Net;
using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.channels;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Handlers.Channels;

public sealed class ExportMessageLinkHandler
{
    private readonly IChatParticipantsRepository _chatParticipantsRepository;

    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChannelMessagesRepository _channelMessagesRepository;
    private readonly IChatRepository _chatRepository;

    private readonly IUnitOfWork _unitOfWork;

    public ExportMessageLinkHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IAuthorizationRepository authorizationRepository, IChannelMessagesRepository channelMessagesRepository, IChatRepository chatRepository)
    {
        _chatParticipantsRepository = chatParticipantsRepository;

        _authorizationRepository = authorizationRepository;
        _channelMessagesRepository = channelMessagesRepository;
        _chatRepository = chatRepository;

        _unitOfWork = unitOfWork;
    }

    [TLFunction(Constructors.baseLayer_ExportMessageLink)]
    public async ValueTask<TLExportedMessageLink> Handle(long authKeyId,
        TLBytes q)
    {
        var request = (ExportMessageLink)q;
        long? channelId = ResolveChannelId(request.Get_ChannelView());
        int messageId = request.Id;
        bool grouped = request.Grouped;
        bool thread = request.Thread;

        long userId;
        using (TLAuthInfo? auth = await _authorizationRepository
                   .GetAuthorizationAsync(authKeyId))
        {
            if (auth == null)
            {
                return Error("AUTH_KEY_INVALID");
            }
            userId = auth.Value.AsAuthInfo().UserId;
        }

        if (channelId is not > 0 || messageId <= 0)
        {
            return Error(channelId is not > 0
                ? "CHANNEL_INVALID"
                : "MESSAGE_ID_INVALID");
        }

        using TLChat? channel = await _chatRepository
            .GetChatAsync(channelId.Value);
        if (channel == null || channel.Value.Type != TLChat.ChatType.Channel)
        {
            return Error("CHANNEL_INVALID");
        }

        using TLChatParticipantInfo? participant = await _chatParticipantsRepository.GetParticipantAsync(channelId.Value,
                userId);
        if (participant == null || !IsActive(participant.Value))
        {
            return Error("USER_NOT_PARTICIPANT");
        }

        using TLSavedMessage? saved = await _channelMessagesRepository
            .GetMessageAsync(channelId.Value, messageId);
        if (saved == null)
        {
            return Error("MESSAGE_ID_INVALID");
        }

        int threadId = thread
            ? ResolveThreadId(saved.Value.AsSavedMessage()
                .Get_OriginalMessage(), messageId)
            : 0;
        string link = BuildLink(channelId.Value, messageId, grouped, threadId);
        string escaped = WebUtility.HtmlEncode(link);
        string html = $"<a href=\"{escaped}\">{escaped}</a>";

        return ExportedMessageLink.Builder()
            .Link(Encoding.UTF8.GetBytes(link))
            .Html(Encoding.UTF8.GetBytes(html))
            .Build();
    }

    internal static string BuildLink(long channelId, int messageId,
        bool grouped, int threadId)
    {
        var link = new StringBuilder("tg://privatepost?channel=")
            .Append(channelId)
            .Append("&post=")
            .Append(messageId);
        if (!grouped)
        {
            link.Append("&single");
        }
        if (threadId > 0)
        {
            link.Append("&thread=").Append(threadId);
        }
        return link.ToString();
    }

    private static int ResolveThreadId(TLMessage message, int fallback)
    {
        if (message.Type == TLMessage.MessageType.Message)
        {
            var regular = message.AsMessage();
            if (regular.Flags[3] && regular.Get_ReplyToView()
                    .Is(out MessageReplyHeader reply))
            {
                return reply.Flags[1]
                    ? reply.ReplyToTopId
                    : reply.ReplyToMsgId;
            }
        }
        else if (message.Type == TLMessage.MessageType.MessageService)
        {
            var service = message.AsMessageService();
            if (service.Flags[3] && service.Get_ReplyToView()
                    .Is(out MessageReplyHeader reply))
            {
                return reply.Flags[1]
                    ? reply.ReplyToTopId
                    : reply.ReplyToMsgId;
            }
        }
        return fallback;
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

    private static TLExportedMessageLink Error(string message) =>
        (TLExportedMessageLink)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));
}
