// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Reactions;

public sealed class ReadReactionsHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ReactionStore _reactions;
    private readonly ICounterFactory _counterFactory;
    private readonly IUpdatesContextFactory _updatesContextFactory;
    private readonly ILogger _log;

    public ReadReactionsHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChatParticipantsRepository chatParticipantsRepository, ReactionStore reactions,
        ICounterFactory counterFactory, IUpdatesContextFactory updatesContextFactory,
        ILogger log)
    {
        _authorizationRepository = authorizationRepository;
        _chatParticipantsRepository = chatParticipantsRepository;

        _unitOfWork = unitOfWork;
        _reactions = reactions;
        _counterFactory = counterFactory;
        _updatesContextFactory = updatesContextFactory;
        _log = log;
    }

    [TLFunction(Constructors.baseLayer_ReadReactions)]
    public async Task<TLAffectedHistory> Handle(long authKeyId, TLBytes q)
    {
        var auth = await _authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        if (auth == null)
        {
            return Error("AUTH_KEY_INVALID");
        }

        long userId = auth.Value.AsAuthInfo().UserId;
        var request = (ReadReactions)q;
        long channelId = PeerResolver.ResolveInputPeerChannelId(request.Get_PeerView());
        (TLPeer.PeerType peerType, long peerId) = PeerResolver.ResolveHistoryPeer(request.Get_PeerView(), userId);
        if (channelId <= 0 && peerId <= 0)
        {
            return Error("PEER_ID_INVALID");
        }

        int cleared;
        if (channelId > 0)
        {
            var participant = await _chatParticipantsRepository
                .GetParticipantAsync(channelId, userId);
            bool activeMember = participant != null && IsActive(participant.Value);
            participant?.Dispose();
            if (!activeMember)
            {
                return Error("CHANNEL_PRIVATE");
            }
            cleared = await _reactions.ClearUnreadReactionRows(
                MessageReactionBox.Channel, channelId, userId,
                requireChannelAuthor: true, rebuildStoredCopies: false);
            await _unitOfWork.SaveAsync();
            var channelBox = new ChannelMessageBox(_counterFactory, channelId);
            int channelPts = await channelBox.Pts();
            _log.Debug($"💟 ReadReactions user:{userId} channel:{channelId} " +
                       $"cleared:{cleared}");
            return AffectedHistory.Builder()
                .Pts(channelPts)
                .PtsCount(0)
                .Offset(0)
                .Build();
        }

        cleared = await _reactions.ClearUnreadReactionRows(
            MessageReactionBox.Common, userId, userId,
            requireChannelAuthor: false, rebuildStoredCopies: true,
            filterPeerType: (int)peerType, filterPeerId: peerId);
        await _unitOfWork.SaveAsync();
        var userCtx = _updatesContextFactory.GetUpdatesContext(authKeyId, userId);
        int pts = cleared > 0 ? await userCtx.IncrementPts() : await userCtx.Pts();
        _log.Debug($"💟 ReadReactions user:{userId} peerType:{peerType} peer:{peerId} " +
                   $"cleared:{cleared}");
        return AffectedHistory.Builder()
            .Pts(pts)
            .PtsCount(cleared > 0 ? 1 : 0)
            .Offset(0)
            .Build();
    }

    private static bool IsActive(TLChatParticipantInfo participant)
    {
        int role = participant.AsChatParticipantInfo().Role;
        return role != (int)ChatParticipantRole.Banned &&
               role != (int)ChatParticipantRole.Left;
    }

    private static TLAffectedHistory Error(string message) =>
        (TLAffectedHistory)RpcErrorGenerator.GenerateError(400,
            System.Text.Encoding.UTF8.GetBytes(message));
}
