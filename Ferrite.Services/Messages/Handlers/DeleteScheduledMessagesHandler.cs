// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.MessageMethods;

/// <summary>
/// Drops queue entries without sending them. The answer is the same
/// `updateDeleteScheduledMessages` a flush produces but with no `sent_messages`,
/// which is how a client tells "gone, never sent" from "gone, here are its real ids"
/// (/api/scheduled-messages). No recipient learns anything, because nothing was ever
/// delivered.
/// </summary>
public sealed class DeleteScheduledMessagesHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IUserRepository _userRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ScheduledMessageStore _scheduled;
    private readonly IUpdatesService _updates;
    private readonly UpdateFanout _fanout;
    private readonly IUpdatesContextFactory _updatesContextFactory;
    private readonly ILogger _log;

    public DeleteScheduledMessagesHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IUserRepository userRepository,
        ScheduledMessageStore scheduled, IUpdatesService updates,
        UpdateFanout fanout, IUpdatesContextFactory updatesContextFactory,
        ILogger log)
    {
        _authorizationRepository = authorizationRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;
        _userRepository = userRepository;

        _unitOfWork = unitOfWork;
        _scheduled = scheduled;
        _updates = updates;
        _fanout = fanout;
        _updatesContextFactory = updatesContextFactory;
        _log = log;
    }

    [TLFunction(Constructors.baseLayer_DeleteScheduledMessages)]
    public async Task<TLUpdates> Handle(long authKeyId, TLBytes q)
    {
        long? principal = await ScheduledQueueAccess.AuthenticateAsync(_authorizationRepository, authKeyId);
        if (principal is not { } userId)
        {
            return Error(400, "AUTH_KEY_INVALID");
        }

        var request = (DeleteScheduledMessages)q;
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

        var removed = new List<int>(requestedIds.Length);
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
            if (entry == null || !_scheduled.Delete(entry.Value))
            {
                continue;
            }
            removed.Add(scheduledId);
        }
        await _unitOfWork.SaveAsync();

        // An id that is already gone is harmless and is still reported as deleted:
        // a replayed delete has to converge on the same client state, and the
        // request carries no way to say "this one was already absent".
        int now = _scheduled.UnixNow();
        var acknowledged = removed.Count > 0 ? removed : seen.ToList();
        using (TLUpdate broadcast = ScheduledMessageStore.BuildDeleteScheduledUpdate(
                   resolved.PeerType, resolved.PeerId, acknowledged))
        {
            // Other sessions of the same user own the same queue and must see it
            // shrink; the caller learns from this result instead.
            await _updates.EnqueueUpdate(resolved.UserId,
                ScheduledMessageStore.BuildDeleteScheduledUpdate(resolved.PeerType,
                    resolved.PeerId, acknowledged),
                UpdateDeliveryScope.ExcludingAuthKeys(new[] { authKeyId }));

            var userIds = new HashSet<long> { resolved.UserId };
            var chatIds = new HashSet<long>();
            if (resolved.PeerType == TLPeer.PeerType.PeerUser)
            {
                userIds.Add(resolved.PeerId);
            }
            else
            {
                chatIds.Add(resolved.PeerId);
            }
            List<byte[]> chats = await _fanout.GetChatBytesForViewerAsync(
                resolved.UserId, chatIds);
            int seq = await _updatesContextFactory
                .GetUpdatesContext(authKeyId, resolved.UserId).IncrementSeq();
            _log.Debug($"⏰ DeleteScheduledMessages user:{resolved.UserId} " +
                       $"peer:{resolved.PeerType}:{resolved.PeerId} " +
                       $"deleted:{removed.Count}");
            return _fanout.BuildUpdates(new[] { broadcast.AsSpan().ToArray() },
                userIds, chats, now, seq);
        }
    }

    private static TLUpdates Error(int code, string message) =>
        (TLUpdates)RpcErrorGenerator.GenerateError(code,
            Encoding.UTF8.GetBytes(message));
}
