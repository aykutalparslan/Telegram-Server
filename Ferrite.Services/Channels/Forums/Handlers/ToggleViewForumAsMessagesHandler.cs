// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.channels;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Handlers.ChannelForums;

public sealed class ToggleViewForumAsMessagesHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;

    private readonly IForumTopicsRepository _forumTopicsRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly UpdateFanout _fanout;

    public ToggleViewForumAsMessagesHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IForumTopicsRepository forumTopicsRepository,
        UpdateFanout fanout)
    {
        _authorizationRepository = authorizationRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;

        _forumTopicsRepository = forumTopicsRepository;

        _unitOfWork = unitOfWork;
        _fanout = fanout;
    }

    [TLFunction(Constructors.baseLayer_ToggleViewForumAsMessages)]
    public async Task<Ferrite.TL.baseLayer.TLUpdates> Handle(long authKeyId, TLBytes q)
    {
        var request = (ToggleViewForumAsMessages)q;
        long? channelId = ChannelForumAccess.ResolveInputChannelId(request.Get_ChannelView());
        bool enabled = request.Enabled;
        var (currentUserId, channelBytes, _, error) =
            await ChannelForumAccess.PrepareForumAccessAsync(_authorizationRepository, _chatRepository, _chatParticipantsRepository, authKeyId, channelId);
        if (error != null)
            return ChannelForumErrors.Updates(Encoding.UTF8.GetBytes(error));

        var stateBuilder = ForumUserState.Builder()
            .ChannelId(channelId!.Value).UserId(currentUserId);
        if (enabled) stateBuilder = stateBuilder.ViewForumAsMessages(true);
        using (TLForumUserState state = stateBuilder.Build())
        {
            _forumTopicsRepository.PutUserState(state);
        }

        byte[] updateBytes;
        using (TLUpdate update = UpdateChannelViewForumAsMessages.Builder()
                   .ChannelId(channelId.Value).Enabled(enabled).Build())
        {
            updateBytes = update.AsSpan().ToArray();
        }
        await _fanout.EnqueueSerializedAsync(currentUserId, updateBytes);
        return await ChannelForumUpdates.BuildForumResultAsync(_unitOfWork, _fanout,
            authKeyId, currentUserId, channelBytes, new[] { updateBytes });
    }
}
