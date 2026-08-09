// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer.channels;

namespace Ferrite.Services.Handlers.ChannelForums;

public sealed class GetForumTopicsByIDHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChannelMessagesRepository _channelMessagesRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IForumTopicsRepository _forumTopicsRepository;
    private readonly IUserRepository _userRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICounterFactory _counterFactory;

    public GetForumTopicsByIDHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IForumTopicsRepository forumTopicsRepository, IUserRepository userRepository,
        ICounterFactory counterFactory)
    {
        _authorizationRepository = authorizationRepository;
        _channelMessagesRepository = channelMessagesRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;
        _forumTopicsRepository = forumTopicsRepository;
        _userRepository = userRepository;

        _unitOfWork = unitOfWork;
        _counterFactory = counterFactory;
    }

    [TLFunction(Constructors.baseLayer_GetForumTopicsByID)]
    public async Task<Ferrite.TL.baseLayer.messages.TLForumTopics> Handle(
        long authKeyId, TLBytes q)
    {
        var request = (GetForumTopicsByID)q;
        long? channelId = ChannelForumAccess.ResolveInputChannelId(request.Get_ChannelView());
        var topics = request.Topics;
        List<int> topicIds = new List<int>(topics.Count);
        for (int i = 0; i < topics.Count; i++) topicIds.Add(topics[i]);
        return await ChannelForumTopics.GetAsync(_authorizationRepository, _chatRepository, _chatParticipantsRepository, _channelMessagesRepository, _forumTopicsRepository, _userRepository, _counterFactory,
            authKeyId, channelId, string.Empty, 0, 0, 0, topicIds.Count, topicIds);
    }
}
