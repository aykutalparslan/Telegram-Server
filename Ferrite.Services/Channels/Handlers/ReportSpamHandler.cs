// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.channels;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Handlers.Channels;

public sealed class ReportSpamHandler
{
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;

    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IUserRepository _userRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ModerationStore _moderation;
    private readonly MessageLocator _messages;

    public ReportSpamHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IAuthorizationRepository authorizationRepository, IUserRepository userRepository, ModerationStore moderation,
        MessageLocator messages)
    {
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;

        _authorizationRepository = authorizationRepository;
        _userRepository = userRepository;

        _unitOfWork = unitOfWork;
        _moderation = moderation;
        _messages = messages;
    }

    [TLFunction(Constructors.baseLayer_ChannelsReportSpam)]
    public async Task<TLBool> Handle(long authKeyId, TLBytes q)
    {
        long userId;
        using (TLAuthInfo? auth = await _authorizationRepository
                   .GetAuthorizationAsync(authKeyId))
        {
            if (auth == null || !auth.Value.AsAuthInfo().LoggedIn)
            {
                return Error("AUTH_KEY_INVALID");
            }
            userId = auth.Value.AsAuthInfo().UserId;
        }

        var request = (ChannelsReportSpam)q;
        long channelId = ResolveChannelId(request.Get_ChannelView());
        bool participantResolved = PeerResolver.TryResolveInputPeerDialogKey(
            request.Get_ParticipantView(), userId, out DialogPeerKey participant);
        List<int> messageIds = ReadIds(request.Id);

        if (channelId <= 0)
        {
            return Error("CHANNEL_INVALID");
        }
        if (!participantResolved ||
            participant.Type != TLPeer.PeerType.PeerUser ||
            participant.Id == userId)
        {
            return Error("USER_ID_INVALID");
        }

        string? accessError = await ChannelAccess.ValidateReadAsync(_chatRepository, _chatParticipantsRepository, channelId, userId);
        if (accessError != null)
        {
            return Error(accessError);
        }

        using (TLUser? accused = _userRepository.GetUser(participant.Id))
        {
            if (accused == null)
            {
                return Error("USER_ID_INVALID");
            }
        }

        foreach (int messageId in messageIds)
        {
            if (await _messages.FindChannelAsync(channelId, messageId) == null)
            {
                return Error("MSG_ID_INVALID");
            }
        }

        long reportId = await _moderation.RecordReportAsync(userId,
            ModerationReportKind.ChannelSpam, TLPeer.PeerType.PeerChannel,
            channelId, messageIds: messageIds, subjectUserId: participant.Id);
        if (reportId == 0 || !await _unitOfWork.SaveAsync())
        {
            return Error("INTERNAL_SERVER_ERROR");
        }
        return BoolTrue.Builder().Build();
    }

    private static long ResolveChannelId(InputChannelView channel)
    {
        if (channel.Is(out InputChannel direct))
        {
            return direct.ChannelId;
        }
        if (channel.Is(out InputChannelFromMessage fromMessage))
        {
            return fromMessage.ChannelId;
        }
        return 0;
    }

    private static List<int> ReadIds(VectorOfInt ids)
    {
        var messageIds = new List<int>(ids.Count);
        for (int i = 0; i < ids.Count; i++)
        {
            if (!messageIds.Contains(ids[i]))
            {
                messageIds.Add(ids[i]);
            }
        }
        return messageIds;
    }

    private static TLBool Error(string message) =>
        (TLBool)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));
}
