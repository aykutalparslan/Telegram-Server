// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

/// <summary>
/// Removes the caller's one-to-one call-log service messages. When revoke is
/// requested, the peer copy is found by the stable call id plus the caller/peer
/// relationship because private message ids are local to each user's box.
/// </summary>
public sealed class DeletePhoneCallHistoryHandler
{
    private readonly IMessageRepository _messageRepository;

    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IUpdatesContextFactory _updatesContextFactory;
    private readonly MessageStore _messages;
    private readonly UpdateFanout _fanout;

    public DeletePhoneCallHistoryHandler(IUnitOfWork unitOfWork, IMessageRepository messageRepository, IAuthorizationRepository authorizationRepository,
        IUpdatesContextFactory updatesContextFactory, MessageStore messages,
        UpdateFanout fanout)
    {
        _messageRepository = messageRepository;

        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
        _updatesContextFactory = updatesContextFactory;
        _messages = messages;
        _fanout = fanout;
    }

    [TLFunction(Constructors.baseLayer_DeletePhoneCallHistory)]
    public async Task<TLAffectedFoundMessages> Handle(long authKeyId, TLBytes q)
    {
        bool revoke = ((DeletePhoneCallHistory)q).Revoke;

        long userId;
        using (TLAuthInfo? auth = await _authorizationRepository
                   .GetAuthorizationAsync(authKeyId))
        {
            if (auth == null || !auth.Value.AsAuthInfo().LoggedIn)
            {
                return (TLAffectedFoundMessages)RpcErrorGenerator.GenerateError(400,
                    "AUTH_KEY_INVALID"u8);
            }
            userId = auth.Value.AsAuthInfo().UserId;
        }

        List<PhoneCallLogEntry> callerEntries = await ReadCallLogAsync(userId);
        List<int> callerIds = callerEntries.Select(x => x.MessageId)
            .Distinct()
            .Order()
            .ToList();

        var peerIds = new Dictionary<long, List<int>>();
        if (revoke && callerEntries.Count > 0)
        {
            foreach (IGrouping<long, PhoneCallLogEntry> peerCalls in callerEntries
                         .GroupBy(x => x.PeerUserId))
            {
                long peerUserId = peerCalls.Key;
                var callIds = peerCalls.Select(x => x.CallId).ToHashSet();
                List<PhoneCallLogEntry> peerEntries = await ReadCallLogAsync(peerUserId);
                List<int> matchingIds = peerEntries
                    .Where(x => x.PeerUserId == userId && callIds.Contains(x.CallId))
                    .Select(x => x.MessageId)
                    .Distinct()
                    .Order()
                    .ToList();
                if (matchingIds.Count > 0)
                {
                    peerIds[peerUserId] = matchingIds;
                }
            }
        }

        if (callerIds.Count > 0)
        {
            _messages.DeleteMessages(userId, callerIds);
            foreach (var peer in peerIds.OrderBy(x => x.Key))
            {
                _messages.DeleteMessages(peer.Key, peer.Value);
            }
            await _unitOfWork.SaveAsync();
        }

        IUpdatesContext callerContext = _updatesContextFactory
            .GetUpdatesContext(authKeyId, userId);
        int pts = await _fanout.AdvanceAndEnqueueDeleteMessagesAsync(userId,
            callerIds, callerContext);
        foreach (var peer in peerIds.OrderBy(x => x.Key))
        {
            IUpdatesContext peerContext = _updatesContextFactory
                .GetUpdatesContext(null, peer.Key);
            await _fanout.AdvanceAndEnqueueDeleteMessagesAsync(peer.Key,
                peer.Value, peerContext);
        }

        var ids = new VectorOfInt();
        foreach (int id in callerIds)
        {
            ids.Append(id);
        }
        return AffectedFoundMessages.Builder()
            .Pts(pts)
            .PtsCount(callerIds.Count)
            .Offset(0)
            .Messages(ids)
            .Build();
    }

    private async Task<List<PhoneCallLogEntry>> ReadCallLogAsync(long ownerId)
    {
        IReadOnlyCollection<TLSavedMessage> saved = await _messageRepository.GetMessagesAsync(ownerId);
        var entries = new List<PhoneCallLogEntry>();
        foreach (TLSavedMessage row in saved)
        {
            using var savedMessage = row;
            TLMessage message = savedMessage.AsSavedMessage().Get_OriginalMessage();
            if (message.Type != TLMessage.MessageType.MessageService ||
                !MessageStore.TryReadStoredMessageInfo(message,
                    out StoredMessageInfo info) ||
                info.PeerType != TLPeer.PeerType.PeerUser || info.PeerId <= 0)
            {
                continue;
            }

            MessageActionView action = message.AsMessageService().Get_ActionView();
            if (action.Is(out MessageActionPhoneCall phoneCall))
            {
                entries.Add(new PhoneCallLogEntry(info.Id, info.PeerId,
                    phoneCall.CallId));
            }
        }
        return entries;
    }

    private readonly record struct PhoneCallLogEntry(int MessageId,
        long PeerUserId, long CallId);
}
