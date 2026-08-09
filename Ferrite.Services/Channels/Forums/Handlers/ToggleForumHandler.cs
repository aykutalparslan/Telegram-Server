// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.channels;
using Ferrite.TL.baseLayer.dto;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.ChannelForums;

public sealed class ToggleForumHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;

    private readonly IForumTopicsRepository _forumTopicsRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger _log;
    private readonly ChatRowStore _chatRows;
    private readonly UpdateFanout _fanout;

    public ToggleForumHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IForumTopicsRepository forumTopicsRepository, ILogger log,
        ChatRowStore chatRows, UpdateFanout fanout)
    {
        _authorizationRepository = authorizationRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;

        _forumTopicsRepository = forumTopicsRepository;

        _unitOfWork = unitOfWork;
        _log = log;
        _chatRows = chatRows;
        _fanout = fanout;
    }

    [TLFunction(Constructors.baseLayer_ToggleForum)]
    public async Task<Ferrite.TL.baseLayer.TLUpdates> Handle(long authKeyId, TLBytes q)
    {
        var request = (ToggleForum)q;
        long? channelId = ChannelForumAccess.ResolveInputChannelId(request.Get_ChannelView());
        bool enabled = request.Enabled;
        bool tabs = request.Tabs;

        var (currentUserId, channelBytes, error) =
            await ChannelForumAccess.PrepareChannelMutationAsync(_authorizationRepository, _chatRepository, _chatParticipantsRepository, authKeyId, channelId, creatorOnly: true);
        if (error != null)
            return ChannelForumErrors.Updates(Encoding.UTF8.GetBytes(error));

        bool megagroup;
        bool currentForum;
        bool currentTabs;
        {
            using var stored = new TLChat(channelBytes, 0, channelBytes.Length);
            var channel = stored.AsChannel();
            megagroup = channel.Megagroup;
            currentForum = channel.Forum;
            currentTabs = channel.ForumTabs;
        }
        if (!megagroup) return ChannelForumErrors.Updates("CHANNEL_INVALID"u8);
        tabs = enabled && tabs;
        if (currentForum == enabled && currentTabs == tabs)
            return ChannelForumErrors.Updates("CHAT_NOT_MODIFIED"u8);

        long id = channelId!.Value;
        byte[] updatedChannelBytes = _chatRows.UpdateStoredChannelForumState(channelBytes,
            enabled, tabs);
        await _chatRows.UpdateStoredChannelForumTabsAsync(id, tabs);
        if (enabled)
        {
            using var existing = await _forumTopicsRepository.GetTopicAsync(id, 1);
            if (existing == null)
            {
                int date = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
                using TLForumTopicInfo general = ForumMessages.BuildStoredForumTopic(id, 1,
                    currentUserId, date, "General"u8.ToArray(), 0x6FB9F0, 0, 1,
                    closed: false, hidden: false, pinnedOrder: 0);
                _forumTopicsRepository.PutTopic(general);
            }
        }

        Ferrite.TL.baseLayer.TLUpdates result =
            await ChannelForumUpdates.BuildChannelResultAsync(_unitOfWork, _fanout,
                authKeyId, currentUserId, updatedChannelBytes, Array.Empty<long>());
        await _fanout.PushUpdateChannelToOtherMembersAsync(id, currentUserId);
        _log.Debug($"📣 ToggleForum user:{currentUserId} channel:{id} enabled:{enabled} tabs:{tabs}");
        return result;
    }
}
