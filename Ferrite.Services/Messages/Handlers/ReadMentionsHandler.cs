// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.MessageMethods;

/// <summary>
/// Marks every unread mention in one dialog (optionally one forum topic) read.
/// Only the caller's own state changes: a common-box copy loses its `mentioned`
/// flag while keeping `media_unread`, so unread voice content survives, and a
/// channel post gains a per-viewer content-read row instead of being mutated.
/// The `offset` of the returned affectedHistory is always 0 because the whole
/// scope is cleared in one pass; the pinned client re-sends the query while the
/// offset is positive.
/// </summary>
public sealed class ReadMentionsHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChannelContentReadsRepository _channelContentReadsRepository;
    private readonly IMessageRepository _messageRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IUpdatesContextFactory _updatesContextFactory;
    private readonly ICounterFactory _counterFactory;
    private readonly DialogBuilder _dialogs;
    private readonly MentionScope _mentions;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _log;

    public ReadMentionsHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChannelContentReadsRepository channelContentReadsRepository, IMessageRepository messageRepository,
        IUpdatesContextFactory updatesContextFactory, ICounterFactory counterFactory,
        DialogBuilder dialogs, MentionScope mentions, TimeProvider timeProvider,
        ILogger log)
    {
        _authorizationRepository = authorizationRepository;
        _channelContentReadsRepository = channelContentReadsRepository;
        _messageRepository = messageRepository;

        _unitOfWork = unitOfWork;
        _updatesContextFactory = updatesContextFactory;
        _counterFactory = counterFactory;
        _dialogs = dialogs;
        _mentions = mentions;
        _timeProvider = timeProvider;
        _log = log;
    }

    [TLFunction(Constructors.baseLayer_ReadMentions)]
    public async Task<TLAffectedHistory> Handle(long authKeyId, TLBytes q)
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

        var request = (ReadMentions)q;
        int topMsgId = request.Flags[0] ? request.TopMsgId : 0;
        long channelId = PeerResolver.ResolveInputPeerChannelId(request.Get_PeerView());
        (TLPeer.PeerType peerType, long peerId) = PeerResolver.ResolveHistoryPeer(request.Get_PeerView(),
            userId);

        return channelId > 0
            ? await ReadChannelMentionsAsync(userId, channelId, topMsgId)
            : await ReadCommonMentionsAsync(authKeyId, userId, peerType, peerId,
                topMsgId);
    }

    private async Task<TLAffectedHistory> ReadCommonMentionsAsync(long authKeyId,
        long userId, TLPeer.PeerType peerType, long peerId, int topMsgId)
    {
        if (peerId <= 0)
        {
            return Error("PEER_ID_INVALID");
        }

        // One pass over the caller's box: the stored row carries both the mention
        // flag and the pts its rewrite has to keep.
        int cleared = 0;
        IReadOnlyCollection<TLSavedMessage> saved = await _messageRepository
            .GetMessagesAsync(userId);
        foreach (TLSavedMessage row in saved)
        {
            using TLSavedMessage stored = row;
            var body = stored.AsSavedMessage();
            using TLMessage original = body.Get_OriginalMessage();
            if (!MessageStore.TryReadStoredMessageInfo(original,
                    out StoredMessageInfo info) ||
                info.PeerType != peerType || info.PeerId != peerId ||
                !MentionScope.IsUnreadCommonMention(info.Bytes, info.Id, topMsgId))
            {
                continue;
            }

            using TLMessage read = original.AsMessage().Clone()
                .Mentioned(false)
                .Build();
            _messageRepository.PutMessage(userId, read, body.Pts);
            cleared++;
        }

        if (cleared > 0 && !await _unitOfWork.SaveAsync())
        {
            return Error("INTERNAL_SERVER_ERROR");
        }

        IUpdatesContext context = _updatesContextFactory.GetUpdatesContext(authKeyId,
            userId);
        int pts = cleared > 0 ? await context.IncrementPts() : await context.Pts();
        _log.Debug($"📣 ReadMentions user:{userId} peer:{peerType}:{peerId} " +
                   $"topic:{topMsgId} cleared:{cleared}");
        return AffectedHistory.Builder()
            .Pts(pts)
            .PtsCount(cleared > 0 ? 1 : 0)
            .Offset(0)
            .Build();
    }

    private async Task<TLAffectedHistory> ReadChannelMentionsAsync(long userId,
        long channelId, int topMsgId)
    {
        string? accessError = await _mentions.ValidateChannelAccessAsync(channelId,
            userId);
        if (accessError != null)
        {
            return Error(accessError);
        }

        List<MessageSnapshot> posts = await _dialogs.ReadChannelConversationAsync(
            channelId);
        List<MessageSnapshot> unread = await _mentions
            .SelectUnreadChannelMentionsAsync(channelId, userId, posts, topMsgId);
        int readAt = checked((int)_timeProvider.GetUtcNow().ToUnixTimeSeconds());
        foreach (MessageSnapshot mention in unread)
        {
            using TLChannelContentRead read = ChannelContentRead.Builder()
                .UserId(userId)
                .ChannelId(channelId)
                .MessageId(mention.Id)
                .ReadAt(readAt)
                .Build();
            _channelContentReadsRepository.PutContentRead(read);
        }
        if (unread.Count > 0 && !await _unitOfWork.SaveAsync())
        {
            return Error("INTERNAL_SERVER_ERROR");
        }

        var channelBox = new ChannelMessageBox(_counterFactory, channelId);
        int pts = await channelBox.Pts();
        _log.Debug($"📣 ReadMentions user:{userId} channel:{channelId} " +
                   $"topic:{topMsgId} cleared:{unread.Count}");
        return AffectedHistory.Builder()
            .Pts(pts)
            .PtsCount(0)
            .Offset(0)
            .Build();
    }

    private static TLAffectedHistory Error(string message) =>
        (TLAffectedHistory)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));
}
