// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.Services.Scheduling;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class SendScheduledMessagesHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IUserRepository _userRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ScheduledMessageStore _scheduled;
    private readonly ScheduledMessageFlusher _flusher;
    private readonly UpdateFanout _fanout;
    private readonly IUpdatesContextFactory _updatesContextFactory;
    private readonly ILogger _log;

    public SendScheduledMessagesHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IUserRepository userRepository,
        ScheduledMessageStore scheduled, ScheduledMessageFlusher flusher,
        UpdateFanout fanout, IUpdatesContextFactory updatesContextFactory,
        ILogger log)
    {
        _authorizationRepository = authorizationRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;
        _userRepository = userRepository;

        _unitOfWork = unitOfWork;
        _scheduled = scheduled;
        _flusher = flusher;
        _updatesContextFactory = updatesContextFactory;
        _fanout = fanout;
        _log = log;
    }

    [TLFunction(Constructors.baseLayer_SendScheduledMessages)]
    public async Task<TLUpdates> Handle(long authKeyId, TLBytes q)
    {
        long? principal = await ScheduledQueueAccess.AuthenticateAsync(_authorizationRepository, authKeyId);
        if (principal is not { } userId)
        {
            return Error(400, "AUTH_KEY_INVALID");
        }

        var request = (SendScheduledMessages)q;
        int[] requestedIds = request.Id.ToArray();
        DialogPeerKey? peer = PeerResolver.ResolveOptionalDialogPeer(request.Get_PeerView(),
            userId);
        ScheduledQueueAccess.Resolved resolved = await ScheduledQueueAccess.ValidateAsync(_userRepository, _chatRepository, _chatParticipantsRepository, userId, peer);
        if (resolved.Error is { } error)
        {
            return Error(error.Code, error.Message);
        }
        if (requestedIds.Length == 0)
        {
            return Error(400, "MESSAGE_IDS_EMPTY");
        }

        int now = _scheduled.UnixNow();
        var flushed = new List<ScheduledMessageFlusher.FlushedMessage>(
            requestedIds.Length);
        var seen = new HashSet<int>();
        foreach (int scheduledId in requestedIds)
        {
            if (!seen.Add(scheduledId))
            {
                continue;
            }
            ScheduledMessageStore.ScheduledSnapshot? entry = await _scheduled
                .GetAsync(resolved.UserId, resolved.PeerType, resolved.PeerId,
                    scheduledId);
            if (entry == null)
            {
                continue;
            }

            ScheduledMessageFlusher.FlushOutcome outcome = await _flusher.FlushAsync(
                authKeyId, entry.Value, now);
            if (outcome.Error is { } failure)
            {
                if (flushed.Count == 0)
                {
                    return Error(failure.Code, failure.Message);
                }
                break;
            }
            flushed.Add(outcome.Message!.Value);
        }

        if (flushed.Count == 0)
        {
            return Error(400, "MESSAGE_ID_INVALID");
        }

        var updateBytes = new List<byte[]>(flushed.Count + 1);
        var userIds = new HashSet<long> { resolved.UserId };
        var chatIds = new HashSet<long>();
        foreach (ScheduledMessageFlusher.FlushedMessage message in flushed)
        {
            using (TLUpdate newMessage =
                   ScheduledMessageFlusher.BuildNewMessageUpdate(message))
            {
                updateBytes.Add(newMessage.AsSpan().ToArray());
            }
            byte[] bytes = message.SenderMessageBytes;
            using var stored = new TLMessage(bytes, 0, bytes.Length);
            MessageStore.AddMessageRelatedPeers(stored, userIds, chatIds);
        }
        using (TLUpdate removal = ScheduledMessageStore.BuildDeleteScheduledUpdate(
                   resolved.PeerType, resolved.PeerId,
                   flushed.Select(x => x.ScheduledId).ToArray(),
                   flushed.Select(x => x.SentMessageId).ToArray()))
        {
            updateBytes.Add(removal.AsSpan().ToArray());
        }

        if (resolved.PeerType == TLPeer.PeerType.PeerUser)
        {
            userIds.Add(resolved.PeerId);
        }
        else
        {
            chatIds.Add(resolved.PeerId);
        }
        List<byte[]> chats = await _fanout.GetChatBytesForViewerAsync(resolved.UserId,
            chatIds);
        int seq = await _updatesContextFactory
            .GetUpdatesContext(authKeyId, resolved.UserId).IncrementSeq();
        _log.Debug($"⏰ SendScheduledMessages user:{resolved.UserId} " +
                   $"peer:{resolved.PeerType}:{resolved.PeerId} sent:{flushed.Count}");
        return _fanout.BuildUpdates(resolved.UserId, updateBytes, userIds, chats, now, seq);
    }

    private static TLUpdates Error(int code, string message) =>
        (TLUpdates)RpcErrorGenerator.GenerateError(code,
            Encoding.UTF8.GetBytes(message));
}
