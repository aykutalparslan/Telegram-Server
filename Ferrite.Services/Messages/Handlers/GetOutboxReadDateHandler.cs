// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

/// <summary>
/// Reports when the recipient of the caller's own private message first read it.
/// The answer is the recipient's stored receipt for the logical message, never
/// the query time, and a receipt older than the retention window is reported as
/// too old rather than as unread.
/// </summary>
public sealed class GetOutboxReadDateHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IUserRepository _userRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly MessageLocator _locator;
    private readonly ReadReceiptStore _receipts;
    private readonly TimeProvider _timeProvider;

    public GetOutboxReadDateHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IUserRepository userRepository, MessageLocator locator,
        ReadReceiptStore receipts, TimeProvider timeProvider)
    {
        _authorizationRepository = authorizationRepository;
        _userRepository = userRepository;

        _unitOfWork = unitOfWork;
        _locator = locator;
        _receipts = receipts;
        _timeProvider = timeProvider;
    }

    [TLFunction(Constructors.baseLayer_GetOutboxReadDate)]
    public async Task<TLOutboxReadDate> Handle(long authKeyId, TLBytes q)
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

        var request = (GetOutboxReadDate)q;
        if (!PeerResolver.TryResolveInputPeerDialogKey(request.Get_PeerView(),
                userId, out DialogPeerKey key) ||
            key.Type != TLPeer.PeerType.PeerUser || key.Id <= 0)
        {
            return Error("PEER_ID_INVALID");
        }
        long peerUserId = key.Id;
        int messageId = request.MsgId;
        // A read date names another person's read; the self dialog has no reader.
        if (peerUserId == userId)
        {
            return Error("PEER_ID_INVALID");
        }
        using (TLUser? peer = _userRepository.GetUser(peerUserId))
        {
            if (peer == null)
            {
                return Error("PEER_ID_INVALID");
            }
        }

        StoredMessageLocation? location = await _locator.FindCommonAsync(userId,
            messageId);
        if (location is not { LogicalId: not null })
        {
            return Error("MESSAGE_ID_INVALID");
        }

        int messageDate;
        {
            byte[] bytes = location.Value.MessageBytes;
            using var stored = new TLMessage(bytes, 0, bytes.Length);
            if (stored.Type != TLMessage.MessageType.Message)
            {
                return Error("MESSAGE_ID_INVALID");
            }
            var message = stored.AsMessage();
            if (!message.OutProperty ||
                !PeerResolver.TryReadPeer(message.Get_PeerIdView(), out var peer) ||
                peer.Type != TLPeer.PeerType.PeerUser || peer.Id != peerUserId)
            {
                return Error("MESSAGE_ID_INVALID");
            }
            messageDate = message.Date;
        }

        int now = checked((int)_timeProvider.GetUtcNow().ToUnixTimeSeconds());
        if (now - messageDate > ReadReceiptStore.ExpirePeriod)
        {
            return Error("MESSAGE_TOO_OLD");
        }

        MessageIdentity identity = MessageIdentity.ForLogical(
            location.Value.LogicalId.Value);
        int? readDate = await _receipts.GetReadDateAsync(identity, peerUserId, now);
        if (readDate == null)
        {
            return Error("MESSAGE_NOT_READ_YET");
        }
        return OutboxReadDate.Builder().Date(readDate.Value).Build();
    }

    private static TLOutboxReadDate Error(string message) =>
        (TLOutboxReadDate)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));
}
