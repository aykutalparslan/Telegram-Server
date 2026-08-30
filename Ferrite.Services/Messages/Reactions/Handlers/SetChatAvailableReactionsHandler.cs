// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Reactions;

public sealed class SetChatAvailableReactionsHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ReactionStore _reactions;
    private readonly UpdateFanout _fanout;
    private readonly IUpdatesContextFactory _updatesContextFactory;
    private readonly ILogger _log;

    public SetChatAvailableReactionsHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository,
        ReactionStore reactions, UpdateFanout fanout,
        IUpdatesContextFactory updatesContextFactory, ILogger log)
    {
        _authorizationRepository = authorizationRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;

        _unitOfWork = unitOfWork;
        _reactions = reactions;
        _fanout = fanout;
        _updatesContextFactory = updatesContextFactory;
        _log = log;
    }

    [TLFunction(Constructors.baseLayer_SetChatAvailableReactions)]
    public async Task<TLUpdates> Handle(long authKeyId, TLBytes q)
    {
        var request = (SetChatAvailableReactions)q;
        var peer = request.Get_PeerView();
        bool toChat = peer.Is(out InputPeerChat chatPeer);
        long chatId = toChat ? chatPeer.ChatId : 0;
        long channelId = PeerResolver.ResolveInputPeerChannelId(peer);
        byte[] availableBytes = request.AvailableReactions.ToArray();
        int? reactionsLimit = request.Flags[0] ? request.ReactionsLimit : null;

        if (!toChat && channelId <= 0)
        {
            return Error("PEER_ID_INVALID");
        }
        if (!ReactionStore.IsValidChatReactionsValue(availableBytes))
        {
            return Error("REACTION_INVALID");
        }

        var auth = await _authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        if (auth == null)
        {
            return Error("AUTH_KEY_INVALID");
        }
        long actorUserId = auth.Value.AsAuthInfo().UserId;

        if (toChat)
        {
            using var storedChat = await _chatRepository.GetChatAsync(chatId);
            if (storedChat == null || storedChat.Value.Type != TLChat.ChatType.Chat ||
                storedChat.Value.AsChat().Deactivated)
            {
                return Error("CHAT_ID_INVALID");
            }
            var participant = await _chatParticipantsRepository
                .GetParticipantAsync(chatId, actorUserId);
            if (participant == null || !IsActive(participant.Value))
            {
                participant?.Dispose();
                return Error("USER_NOT_PARTICIPANT");
            }
            int role = participant.Value.AsChatParticipantInfo().Role;
            participant.Value.Dispose();
            if (role != (int)ChatParticipantRole.Creator &&
                role != (int)ChatParticipantRole.Admin)
            {
                return Error("CHAT_ADMIN_REQUIRED");
            }

            var allParticipants = await _chatParticipantsRepository
                .GetParticipantsAsync(chatId);
            foreach (var participantInfo in allParticipants)
            {
                participantInfo.Dispose();
            }
        }
        else
        {
            using var channel = await _chatRepository.GetChatAsync(channelId);
            if (channel == null || channel.Value.Type != TLChat.ChatType.Channel)
            {
                return Error("CHANNEL_INVALID");
            }
            var participant = await _chatParticipantsRepository
                .GetParticipantAsync(channelId, actorUserId);
            if (participant == null || !IsActive(participant.Value))
            {
                participant?.Dispose();
                return Error("USER_NOT_PARTICIPANT");
            }
            bool canChangeInfo = ChatRights.HasAdminRight(participant.Value,
                ChatAdminRightRequirement.ChangeInfo);
            participant.Value.Dispose();
            if (!canChangeInfo)
            {
                return Error("CHAT_ADMIN_REQUIRED");
            }
        }

        long configChatId = toChat ? chatId : channelId;
        await _reactions.PutChatReactionConfig(configChatId, availableBytes,
            reactionsLimit);
        await _unitOfWork.SaveAsync();
        _log.Debug($"💟 SetChatAvailableReactions user:{actorUserId} chat:{configChatId} " +
                   $"limit:{reactionsLimit?.ToString() ?? "keep"}");

        if (!toChat)
        {
            var memberIds = await _fanout.GetOtherActiveChannelMemberIdsAsync(channelId,
                actorUserId);
            foreach (long memberId in memberIds)
            {
                await _fanout.EnqueueUpdateChannelAsync(memberId, channelId);
            }
        }

        var seqCtx = _updatesContextFactory.GetUpdatesContext(authKeyId, actorUserId);
        int seq = await seqCtx.IncrementSeq();
        List<byte[]> chatBytes = await _fanout.GetChatBytesForViewerAsync(actorUserId,
            new[] { configChatId });
        var userVector = new Vector();
        _fanout.AppendUsers(actorUserId, ref userVector, new[] { actorUserId });
        var chatVector = new Vector();
        foreach (byte[] chatRow in chatBytes)
        {
            chatVector.AppendTLObject(chatRow);
        }

        return Ferrite.TL.baseLayer.Updates.Builder()
            .UpdatesProperty(new Vector())
            .Users(userVector)
            .Chats(chatVector)
            .Date((int)DateTimeOffset.Now.ToUnixTimeSeconds())
            .Seq(seq)
            .Build();
    }

    private static bool IsActive(TLChatParticipantInfo participant)
    {
        int role = participant.AsChatParticipantInfo().Role;
        return role != (int)ChatParticipantRole.Banned &&
               role != (int)ChatParticipantRole.Left;
    }

    private static TLUpdates Error(string message) =>
        (TLUpdates)RpcErrorGenerator.GenerateError(400,
            System.Text.Encoding.UTF8.GetBytes(message));
}
