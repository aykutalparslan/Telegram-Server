// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class GetRecentLocationsHandler
{
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;

    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly DialogBuilder _dialogs;
    private readonly TimeProvider _timeProvider;

    public GetRecentLocationsHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IAuthorizationRepository authorizationRepository, DialogBuilder dialogs,
        TimeProvider timeProvider)
    {
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;

        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
        _dialogs = dialogs;
        _timeProvider = timeProvider;
    }

    [TLFunction(Constructors.baseLayer_GetRecentLocations)]
    public async Task<TLMessages> Handle(long authKeyId, TLBytes q)
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

        var request = (GetRecentLocations)q;
        int limit = request.Limit;
        long hash = request.Hash;
        long channelId = PeerResolver.ResolveInputPeerChannelId(request.Get_PeerView());
        (TLPeer.PeerType peerType, long peerId) = PeerResolver.ResolveHistoryPeer(request.Get_PeerView(),
            userId);

        int now = checked((int)_timeProvider.GetUtcNow().ToUnixTimeSeconds());
        List<MessageSnapshot> conversation;
        if (channelId > 0)
        {
            string? accessError = await ChannelAccess.ValidateReadAsync(_chatRepository, _chatParticipantsRepository, channelId, userId);
            if (accessError != null)
            {
                return Error(accessError);
            }
            conversation = await _dialogs.ReadChannelConversationAsync(channelId);
        }
        else if (peerId > 0)
        {
            conversation = await _dialogs.ReadCommonConversationAsync(userId, peerType,
                peerId);
        }
        else
        {
            return Error("PEER_ID_INVALID");
        }

        List<MessageSnapshot> live = SelectActiveLiveLocations(conversation, now,
            limit);
        if (hash != 0 &&
            hash == TelegramListHash.Compute(live.Select(x => (long)x.Id)))
        {
            return MessagesNotModified.Builder().Count(live.Count).Build();
        }

        var query = new HistoryQuery(0, 0, 0, live.Count, 0, 0);
        return channelId > 0
            ? await _dialogs.BuildChannelMessagesAsync(userId, channelId, live, query,
                live.Count, "GetRecentLocations")
            : await _dialogs.BuildCommonMessagesAsync(userId, peerType, peerId, live,
                query, "GetRecentLocations");
    }

    private static List<MessageSnapshot> SelectActiveLiveLocations(
        IReadOnlyList<MessageSnapshot> conversation, int now, int limit)
    {
        var live = new List<MessageSnapshot>();
        foreach (MessageSnapshot snapshot in conversation)
        {
            if (limit > 0 && live.Count >= limit)
            {
                break;
            }

            byte[] bytes = snapshot.Bytes;
            using var stored = new TLMessage(bytes, 0, bytes.Length);
            if (stored.Type != TLMessage.MessageType.Message)
            {
                continue;
            }
            var message = stored.AsMessage();
            if (message.Get_MediaView().Is(out MessageMediaGeoLive geoLive) &&
                message.Date + geoLive.Period > now)
            {
                live.Add(snapshot);
            }
        }
        return live;
    }

    private static TLMessages Error(string message) =>
        (TLMessages)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));
}
