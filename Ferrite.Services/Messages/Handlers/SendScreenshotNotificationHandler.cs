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
/// Records that the caller screenshotted a private conversation. A screenshot
/// notification is an ordinary private service message, so it is written into
/// both per-owner boxes with their own local ids and pts, and the peer receives
/// updateNewMessage. The result must contain exactly one new message and one
/// updateMessageID for the request's random id, because the pinned client
/// otherwise treats the send as failed and schedules getDifference.
/// </summary>
public sealed class SendScreenshotNotificationHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IUserRepository _userRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly MessageStore _messages;
    private readonly UpdateFanout _fanout;
    private readonly IUpdatesContextFactory _updatesContextFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _log;

    public SendScreenshotNotificationHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IUserRepository userRepository,
        MessageStore messages, UpdateFanout fanout,
        IUpdatesContextFactory updatesContextFactory, TimeProvider timeProvider,
        ILogger log)
    {
        _authorizationRepository = authorizationRepository;
        _userRepository = userRepository;

        _unitOfWork = unitOfWork;
        _messages = messages;
        _fanout = fanout;
        _updatesContextFactory = updatesContextFactory;
        _timeProvider = timeProvider;
        _log = log;
    }

    [TLFunction(Constructors.baseLayer_SendScreenshotNotification)]
    public async Task<TLUpdates> Handle(long authKeyId, TLBytes q)
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

        var request = (SendScreenshotNotification)q;
        if (!PeerResolver.TryResolveInputPeerDialogKey(request.Get_PeerView(),
                userId, out DialogPeerKey key) ||
            key.Type != TLPeer.PeerType.PeerUser || key.Id <= 0)
        {
            return Error("PEER_ID_INVALID");
        }
        long peerUserId = key.Id;
        long randomId = request.RandomId;
        int replyToMsgId = ReadReplyToMsgId(request.Get_ReplyToView());

        using (TLUser? peer = _userRepository.GetUser(peerUserId))
        {
            if (peer == null || peer.Value.Type != TLUser.UserType.User)
            {
                return Error("PEER_ID_INVALID");
            }
        }
        // A screenshot notification tells the other side something happened, so a
        // self dialog has no peer to notify and no service message to record.
        if (peerUserId == userId)
        {
            return Error("PEER_ID_INVALID");
        }

        byte[] actionBytes;
        using (TLMessageAction action = MessageActionScreenshotTaken.Builder().Build())
        {
            actionBytes = action.AsSpan().ToArray();
        }
        byte[]? callerReplyTo = BuildReplyToHeader(replyToMsgId);

        int date = checked((int)_timeProvider.GetUtcNow().ToUnixTimeSeconds());
        StoredMessageWrite callerWrite = await _messages.PutPrivateServiceMessageAsync(
            userId, authKeyId, peerUserId, userId, outgoing: true, actionBytes, date,
            callerReplyTo);
        // The peer's copy of the screenshotted message has a different local id, so
        // the reply pointer is not carried across boxes.
        StoredMessageWrite peerWrite = await _messages.PutPrivateServiceMessageAsync(
            peerUserId, null, userId, userId, outgoing: false, actionBytes, date);
        long logicalId = await _messages.CreateMessageCopyAsync(userId, callerWrite.Id);
        _messages.PutMessageCopy(logicalId, peerUserId, peerWrite.Id);
        if (!await _unitOfWork.SaveAsync())
        {
            return Error("INTERNAL_SERVER_ERROR");
        }

        await _fanout.EnqueueNewMessageAsync(peerUserId, peerWrite.Bytes, peerWrite.Pts);

        int seq = await _updatesContextFactory.GetUpdatesContext(authKeyId, userId)
            .IncrementSeq();
        var updateBytes = new List<byte[]>(2);
        using (TLUpdate updateMessageId = UpdateMessageID.Builder()
                   .Id(callerWrite.Id)
                   .RandomId(randomId)
                   .Build())
        {
            updateBytes.Add(updateMessageId.AsSpan().ToArray());
        }
        using (TLUpdate updateNewMessage = UpdateNewMessage.Builder()
                   .Message(callerWrite.Bytes)
                   .Pts(callerWrite.Pts)
                   .PtsCount(1)
                   .Build())
        {
            updateBytes.Add(updateNewMessage.AsSpan().ToArray());
        }

        _log.Debug($"📸 SendScreenshotNotification user:{userId} peer:{peerUserId} " +
                   $"id:{callerWrite.Id} pts:{callerWrite.Pts}");
        return _fanout.BuildUpdates(updateBytes, new[] { userId, peerUserId },
            Array.Empty<byte[]>(), date, seq);
    }

    private static byte[]? BuildReplyToHeader(int replyToMsgId)
    {
        if (replyToMsgId <= 0)
        {
            return null;
        }
        using TLMessageReplyHeader header = MessageReplyHeader.Builder()
            .ReplyToMsgId(replyToMsgId)
            .Build();
        return header.AsSpan().ToArray();
    }

    private static int ReadReplyToMsgId(InputReplyToView replyTo) =>
        replyTo.Is(out InputReplyToMessage message) ? message.ReplyToMsgId : 0;

    private static TLUpdates Error(string message) =>
        (TLUpdates)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));
}
