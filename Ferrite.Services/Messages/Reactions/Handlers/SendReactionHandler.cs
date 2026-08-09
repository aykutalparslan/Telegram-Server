// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.Reactions;

public sealed class SendReactionHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;

    private const int DefaultUniqueReactionsLimit = 11;
    private const int MaxChosenReactionsPerUser = 3;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ReactionStore _reactions;
    private readonly IdAllocators _ids;
    private readonly SendPipeline _send;
    private readonly UpdateFanout _fanout;

    public SendReactionHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, ReactionStore reactions,
        IdAllocators ids, SendPipeline send, UpdateFanout fanout)
    {
        _authorizationRepository = authorizationRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;

        _unitOfWork = unitOfWork;
        _reactions = reactions;
        _ids = ids;
        _send = send;
        _fanout = fanout;
    }

    [TLFunction(Constructors.baseLayer_MessagesSendReaction)]
    public async Task<TLUpdates> Handle(long authKeyId, TLBytes q)
    {
        var auth = await _authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        if (auth == null)
        {
            return Error("AUTH_KEY_INVALID");
        }

        long userId = auth.Value.AsAuthInfo().UserId;
        bool big;
        bool addToRecent;
        int msgId;
        long channelId;
        TLPeer.PeerType peerType;
        long peerId;
        List<byte[]> requested;
        byte[] callerPeerBytes;
        {
            var request = (MessagesSendReaction)q;
            big = request.Big;
            addToRecent = request.AddToRecent;
            msgId = request.MsgId;
            channelId = PeerResolver.ResolveInputPeerChannelId(request.Get_PeerView());
            (peerType, peerId) = PeerResolver.ResolveHistoryPeer(request.Get_PeerView(), userId);
            requested = request.Flags[0]
                ? ReadReactionVector(request.Reaction)
                : new List<byte[]>();
            using TLPeer callerPeer = channelId > 0
                ? new PeerChannel(channelId)
                : PeerResolver.BuildPeer(peerType, peerId);
            callerPeerBytes = callerPeer.AsSpan().ToArray();
        }

        if (channelId <= 0 && peerId <= 0)
        {
            return Error("PEER_ID_INVALID");
        }

        if (requested.Count > MaxChosenReactionsPerUser)
        {
            requested = requested.Skip(requested.Count - MaxChosenReactionsPerUser).ToList();
        }

        bool broadcast = false;
        if (channelId > 0)
        {
            using var channel = await _chatRepository.GetChatAsync(channelId);
            if (channel == null || channel.Value.Type != TLChat.ChatType.Channel)
            {
                return Error("CHANNEL_INVALID");
            }
            broadcast = channel.Value.AsChannel().Broadcast;
            var participant = await _chatParticipantsRepository
                .GetParticipantAsync(channelId, userId);
            bool activeMember = participant != null && IsActive(participant.Value);
            participant?.Dispose();
            if (!activeMember)
            {
                return Error("CHANNEL_PRIVATE");
            }
        }

        long reactionConfigChatId = channelId > 0
            ? channelId
            : peerType == TLPeer.PeerType.PeerChat ? peerId : 0;
        byte[]? availableBytes = null;
        int reactionsLimit = 0;
        if (reactionConfigChatId > 0)
        {
            (availableBytes, reactionsLimit) =
                await _reactions.GetStoredChatReactionConfigAsync(reactionConfigChatId);
        }

        if (!ReactionStore.AreReactionsAllowed(requested, availableBytes))
        {
            return Error("REACTION_INVALID");
        }

        int date = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
        long order = await _ids.NextReactionOrderAsync();
        int uniqueLimit = reactionsLimit > 0 ? reactionsLimit : DefaultUniqueReactionsLimit;
        PipelineResult<ReactionCallerBatch> sent = channelId > 0
            ? await _send.SendChannelReactionAsync(userId, channelId, msgId, requested,
                big, broadcast, uniqueLimit, date, order, callerPeerBytes)
            : await _send.SendCommonBoxReactionAsync(userId, peerType, peerId, msgId,
                requested, big, uniqueLimit, date, order, callerPeerBytes);
        if (sent.Error != null)
        {
            return Error(sent.Error);
        }

        ReactionCallerBatch batch = sent.Value!;
        TLUpdates result = await _fanout.BuildReactionsResultAsync(batch.UserId,
            batch.PeerBytes, batch.MsgId, batch.ReactionsBytes, batch.Entries,
            batch.ReactionConfigChatId, incrementSeq: true, authKeyId: authKeyId);

        if (addToRecent && requested.Count > 0 &&
            result.Constructor == Constructors.baseLayer_Updates)
        {
            await UpdateRecentReactionsList(userId, requested);
        }

        return result;
    }

    private async Task UpdateRecentReactionsList(long userId, List<byte[]> reactions)
    {
        var (defaultReaction, recent) = await _reactions.ReadReactionSettingsAsync(userId);
        var merged = new List<byte[]>(reactions);
        foreach (byte[] existing in recent)
        {
            if (!merged.Any(r => r.AsSpan().SequenceEqual(existing)))
            {
                merged.Add(existing);
            }
        }
        const int recentLimit = 50;
        if (merged.Count > recentLimit)
        {
            merged = merged.Take(recentLimit).ToList();
        }

        _reactions.PutReactionSettings(userId, defaultReaction, merged);
        await _unitOfWork.SaveAsync();
        await _fanout.EnqueueRecentReactionsAsync(userId);
    }

    private static List<byte[]> ReadReactionVector(Vector vector)
    {
        var reactions = new List<byte[]>();
        int count = vector.Count;
        for (int i = 0; i < count; i++)
        {
            reactions.Add(vector.ReadTLObject().ToArray());
        }
        return reactions;
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
