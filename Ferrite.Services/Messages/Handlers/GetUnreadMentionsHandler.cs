// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class GetUnreadMentionsHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly DialogBuilder _dialogs;
    private readonly MentionScope _mentions;

    public GetUnreadMentionsHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, DialogBuilder dialogs,
        MentionScope mentions)
    {
        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
        _dialogs = dialogs;
        _mentions = mentions;
    }

    [TLFunction(Constructors.baseLayer_GetUnreadMentions)]
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

        var request = (GetUnreadMentions)q;
        HistoryQuery query = new HistoryQuery(request.OffsetId, 0, request.AddOffset,
            request.Limit, request.MaxId, request.MinId);
        int topMsgId = request.Flags[0] ? request.TopMsgId : 0;
        long channelId = PeerResolver.ResolveInputPeerChannelId(request.Get_PeerView());
        (TLPeer.PeerType peerType, long peerId) = PeerResolver.ResolveHistoryPeer(request.Get_PeerView(),
            userId);

        if (channelId > 0)
        {
            string? membershipError = await _mentions.ValidateChannelAccessAsync(
                channelId, userId);
            if (membershipError != null)
            {
                return Error(membershipError);
            }

            List<MessageSnapshot> posts = await _dialogs.ReadChannelConversationAsync(
                channelId);
            List<MessageSnapshot> mentions = await _mentions
                .SelectUnreadChannelMentionsAsync(channelId, userId, posts, topMsgId);
            return await _dialogs.BuildChannelMessagesAsync(userId, channelId,
                mentions, query, mentions.Count, "GetUnreadMentions");
        }

        if (peerId <= 0)
        {
            return Error("PEER_ID_INVALID");
        }

        List<MessageSnapshot> conversation = await _dialogs
            .ReadCommonConversationAsync(userId, peerType, peerId);
        List<MessageSnapshot> unread = MentionScope.SelectUnreadCommonMentions(
            conversation, topMsgId);
        return await _dialogs.BuildCommonMessagesAsync(userId, peerType, peerId,
            unread, query, "GetUnreadMentions");
    }

    private static TLMessages Error(string message) =>
        (TLMessages)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));
}
