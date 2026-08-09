// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.channels;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Handlers.Channels;

public sealed class GetMessageAuthorHandler
{
    private readonly IChatParticipantsRepository _chatParticipantsRepository;

    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChannelMessagesRepository _channelMessagesRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IUserRepository _userRepository;

    private readonly IUnitOfWork _unitOfWork;

    public GetMessageAuthorHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IAuthorizationRepository authorizationRepository, IChannelMessagesRepository channelMessagesRepository, IChatRepository chatRepository, IUserRepository userRepository)
    {
        _chatParticipantsRepository = chatParticipantsRepository;

        _authorizationRepository = authorizationRepository;
        _channelMessagesRepository = channelMessagesRepository;
        _chatRepository = chatRepository;
        _userRepository = userRepository;

        _unitOfWork = unitOfWork;
    }

    [TLFunction(Constructors.baseLayer_GetMessageAuthor)]
    public async ValueTask<TLUser> Handle(long authKeyId, TLBytes q)
    {
        var request = (GetMessageAuthor)q;
        long? channelId = ResolveChannelId(request.Get_ChannelView());
        int messageId = request.Id;

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
        if (!channel.Value.AsChannel().Monoforum)
        {
            return Error("CHANNEL_MONOFORUM_REQUIRED");
        }

        using TLChatParticipantInfo? participant = await _chatParticipantsRepository.GetParticipantAsync(channelId.Value,
                userId);
        if (participant == null || !ChatRights.HasAdminRight(participant.Value,
                ChatAdminRightRequirement.Any))
        {
            return Error("CHAT_ADMIN_REQUIRED");
        }

        using TLSavedMessage? saved = await _channelMessagesRepository
            .GetMessageAsync(channelId.Value, messageId);
        if (saved == null)
        {
            return Error("MESSAGE_ID_INVALID");
        }

        long authorId = ResolveUserAuthor(saved.Value.AsSavedMessage()
            .Get_OriginalMessage());
        if (authorId <= 0)
        {
            return Error("MESSAGE_AUTHOR_REQUIRED");
        }

        TLUser? author = _userRepository.GetUser(authorId);
        return author ?? Error("USER_ID_INVALID");
    }

    private static long ResolveUserAuthor(TLMessage message)
    {
        if (message.Type == TLMessage.MessageType.Message)
        {
            var regular = message.AsMessage();
            return regular.Flags[8] && regular.Get_FromIdView()
                .Is(out PeerUser author)
                ? author.UserId
                : 0;
        }
        if (message.Type == TLMessage.MessageType.MessageService)
        {
            var service = message.AsMessageService();
            return service.Flags[8] && service.Get_FromIdView()
                .Is(out PeerUser author)
                ? author.UserId
                : 0;
        }
        return 0;
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

    private static TLUser Error(string message) =>
        (TLUser)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));
}
