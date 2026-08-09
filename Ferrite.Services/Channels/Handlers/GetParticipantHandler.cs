// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using System.Text.RegularExpressions;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.channels;
using Ferrite.TL.baseLayer.dto;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

public sealed class GetParticipantHandler : ChannelsHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;

    public GetParticipantHandler(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository, ICounterFactory counterFactory,
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

    [TLFunction(Constructors.baseLayer_GetParticipant)]
    public async Task<Ferrite.TL.baseLayer.channels.TLChannelParticipant> Handle(
        long authKeyId, TLBytes q)
    {
        var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
        if (auth == null)
        {
            return (Ferrite.TL.baseLayer.channels.TLChannelParticipant)RpcErrorGenerator
                .GenerateError(400, "AUTH_KEY_INVALID"u8);
        }

        long currentUserId = auth.Value.AsAuthInfo().UserId;
        var request = (GetParticipant)q;
        long? channelId = ResolveInputChannelId(request.Get_ChannelView());
        long? participantId = ResolveInputPeerUserId(request.Get_ParticipantView(), currentUserId);
        if (channelId is not > 0)
        {
            return (Ferrite.TL.baseLayer.channels.TLChannelParticipant)RpcErrorGenerator
                .GenerateError(400, "CHANNEL_INVALID"u8);
        }
        if (participantId is not > 0)
        {
            return (Ferrite.TL.baseLayer.channels.TLChannelParticipant)RpcErrorGenerator
                .GenerateError(400, "PARTICIPANT_ID_INVALID"u8);
        }

        using var channel = await _chatRepository.GetChatAsync(channelId.Value);
        if (channel == null || channel.Value.Type != TLChat.ChatType.Channel)
        {
            return (Ferrite.TL.baseLayer.channels.TLChannelParticipant)RpcErrorGenerator
                .GenerateError(400, "CHANNEL_INVALID"u8);
        }

        using var stored = await _chatParticipantsRepository
            .GetParticipantAsync(channelId.Value, participantId.Value);
        if (stored == null)
        {
            return (Ferrite.TL.baseLayer.channels.TLChannelParticipant)RpcErrorGenerator
                .GenerateError(400, "USER_NOT_PARTICIPANT"u8);
        }

        byte[] participantBytes = BuildChannelParticipantBytes(stored.Value, currentUserId);
        var chatVector = new Vector();
        chatVector.AppendTLObject(channel.Value.AsSpan());
        var userVector = new Vector();
        AppendUser(ref userVector, participantId.Value);

        _log.Debug($"📣 GetParticipant channel:{channelId.Value} participant:{participantId.Value}");

        return Ferrite.TL.baseLayer.channels.ChannelsChannelParticipant.Builder()
            .Participant(participantBytes)
            .Chats(chatVector)
            .Users(userVector)
            .Build();
    }
}
