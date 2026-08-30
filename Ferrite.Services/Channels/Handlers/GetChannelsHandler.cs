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

public sealed class GetChannelsHandler : ChannelsHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;

    public GetChannelsHandler(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository, ICounterFactory counterFactory,
        IdAllocators ids, IUpdatesContextFactory updatesContextFactory,
        IUpdatesService updates, ISearchEngine search, IUploadService upload,
        IPhotoProcessingService photos, ILogger log, ChatRowStore chatRows,
        UpdateFanout fanout)
        : base(unitOfWork, channelMessagesRepository, authorizationRepository, channelAdminLogRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, counterFactory, ids, updatesContextFactory, updates,
            search, upload, photos, log, chatRows, fanout)
    {
        _authorizationRepository = authorizationRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;

    }

    [TLFunction(Constructors.baseLayer_GetChannels)]
    public async Task<Ferrite.TL.baseLayer.messages.TLChats> Handle(long authKeyId, TLBytes q)
    {
        var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
        if (auth == null)
        {
            return (Ferrite.TL.baseLayer.messages.TLChats)RpcErrorGenerator.GenerateError(400, "AUTH_KEY_INVALID"u8);
        }

        var requestIds = ((GetChannels)q).Id;
        var channelIds = new List<long>(requestIds.Count);
        for (int i = 0; i < requestIds.Count; i++)
        {
            InputChannelView channel = requestIds.ReadTLObject();
            long? channelId = ResolveInputChannelId(channel);
            if (channelId is > 0)
            {
                channelIds.Add(channelId.Value);
            }
        }

        long viewerUserId = auth.Value.AsAuthInfo().UserId;
        var chatBytes = new List<byte[]>();
        foreach (long channelId in channelIds)
        {
            using var chat = await _chatRepository.GetChatAsync(channelId);
            if (chat is { Type: TLChat.ChatType.Channel })
            {
                chatBytes.Add(await ChannelRows.ForViewerAsync(
                    _chatParticipantsRepository, viewerUserId, channelId,
                    chat.Value.AsSpan().ToArray()));
            }
        }

        var chatVector = new Vector();
        foreach (byte[] bytes in chatBytes)
        {
            chatVector.AppendTLObject(bytes);
        }

        return Ferrite.TL.baseLayer.messages.Chats.Builder()
            .ChatsProperty(chatVector)
            .Build();
    }
}
