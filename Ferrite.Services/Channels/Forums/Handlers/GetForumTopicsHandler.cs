// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer.channels;

namespace Ferrite.Services.Handlers.ChannelForums;

public sealed class GetForumTopicsHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChannelMessagesRepository _channelMessagesRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IForumTopicsRepository _forumTopicsRepository;
    private readonly UserSerializer _userSerializer;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICounterFactory _counterFactory;

    public GetForumTopicsHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IForumTopicsRepository forumTopicsRepository, UserSerializer userSerializer,
        ICounterFactory counterFactory)
    {
        _authorizationRepository = authorizationRepository;
        _channelMessagesRepository = channelMessagesRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;
        _forumTopicsRepository = forumTopicsRepository;
        _userSerializer = userSerializer;

        _unitOfWork = unitOfWork;
        _counterFactory = counterFactory;
    }

    [TLFunction(Constructors.baseLayer_GetForumTopics)]
    public async Task<Ferrite.TL.baseLayer.messages.TLForumTopics> Handle(
        long authKeyId, TLBytes q)
    {
        var request = (GetForumTopics)q;
        long? channelId = ChannelForumAccess.ResolveInputChannelId(request.Get_ChannelView());
        string query = request.Flags[0] ? Encoding.UTF8.GetString(request.Q) : string.Empty;
        int offsetDate = request.OffsetDate;
        int offsetId = request.OffsetId;
        int offsetTopic = request.OffsetTopic;
        int limit = request.Limit;
        return await ChannelForumTopics.GetAsync(_authorizationRepository, _chatRepository, _chatParticipantsRepository, _channelMessagesRepository, _forumTopicsRepository, _userSerializer, _counterFactory,
            authKeyId, channelId, query, offsetDate, offsetId, offsetTopic, limit, null);
    }
}
