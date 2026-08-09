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
/// Marks the content of common-box messages read. The request carries only the
/// caller's own local ids, so each id is resolved in the caller's box and the
/// mention/media-unread flags are cleared there. Every other live copy of the
/// same logical message keeps its own flags, except the sender's outgoing copy:
/// clearing its media-unread flag is exactly what the peer notification reports,
/// and the notification carries the peer's own copy ids because a client resolves
/// updateReadMessagesContents in its own id space.
/// </summary>
public sealed class ReadMessageContentsHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IMessageRepository _messageRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IUpdatesService _updates;
    private readonly IUpdatesContextFactory _updatesContextFactory;
    private readonly MessageLocator _locator;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _log;

    public ReadMessageContentsHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IMessageRepository messageRepository, IUpdatesService updates,
        IUpdatesContextFactory updatesContextFactory, MessageLocator locator,
        TimeProvider timeProvider, ILogger log)
    {
        _authorizationRepository = authorizationRepository;
        _messageRepository = messageRepository;

        _unitOfWork = unitOfWork;
        _updates = updates;
        _updatesContextFactory = updatesContextFactory;
        _locator = locator;
        _timeProvider = timeProvider;
        _log = log;
    }

    [TLFunction(Constructors.baseLayer_MessagesReadMessageContents)]
    public async Task<TLAffectedMessages> Handle(long authKeyId, TLBytes q)
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

        List<int> requestedIds = ReadRequestedIds(q);
        var callerCtx = _updatesContextFactory.GetUpdatesContext(authKeyId, userId);
        var callerIds = new List<int>();
        var peerIds = new Dictionary<long, List<int>>();
        foreach (int messageId in requestedIds)
        {
            IReadOnlyList<StoredMessageLocation> copies =
                await _locator.FindCommonCopiesAsync(userId, messageId);
            if (!TryClearCallerCopy(copies, userId, messageId))
            {
                continue;
            }

            callerIds.Add(messageId);
            ClearSenderCopies(copies, userId, peerIds);
        }

        if (callerIds.Count == 0)
        {
            int currentPts = await callerCtx.Pts();
            return AffectedMessages.Builder().Pts(currentPts).PtsCount(0).Build();
        }
        if (!await _unitOfWork.SaveAsync())
        {
            return Error("INTERNAL_SERVER_ERROR");
        }

        // One read event advances the box by a single pts step regardless of how
        // many message ids it covers, so pts_count is 1 on every side.
        // EnqueueUpdate owns the value it is handed and disposes it, so these
        // pooled updates are transferred rather than scoped with `using`.
        int date = checked((int)_timeProvider.GetUtcNow().ToUnixTimeSeconds());
        int pts = await callerCtx.IncrementPts();
        await _updates.EnqueueUpdate(userId, BuildUpdate(callerIds, pts, date));

        foreach ((long peerUserId, List<int> ids) in peerIds)
        {
            var peerCtx = _updatesContextFactory.GetUpdatesContext(null, peerUserId);
            int peerPts = await peerCtx.IncrementPts();
            await _updates.EnqueueUpdate(peerUserId, BuildUpdate(ids, peerPts, date));
        }

        _log.Debug($"👂 ReadMessageContents user:{userId} ids:{callerIds.Count} " +
                   $"pts:{pts} peers:{peerIds.Count}");
        return AffectedMessages.Builder().Pts(pts).PtsCount(1).Build();
    }

    /// <summary>
    /// Clears the caller's own copy when it is an incoming regular message that
    /// still carries a mention or unread media. The unread-mention count is
    /// derived from these stored flags, so clearing them is the whole update.
    /// </summary>
    private bool TryClearCallerCopy(IReadOnlyList<StoredMessageLocation> copies,
        long userId, int messageId)
    {
        foreach (StoredMessageLocation copy in copies)
        {
            if (copy.OwnerId != userId || copy.MessageId != messageId)
            {
                continue;
            }

            byte[] bytes = copy.MessageBytes;
            using var stored = new TLMessage(bytes, 0, bytes.Length);
            if (stored.Type != TLMessage.MessageType.Message)
            {
                return false;
            }
            var message = stored.AsMessage();
            if (message.OutProperty || (!message.Mentioned && !message.MediaUnread))
            {
                return false;
            }

            using TLMessage cleared = message.Clone()
                .Mentioned(false)
                .MediaUnread(false)
                .Build();
            _messageRepository.PutMessage(userId, cleared, copy.Pts);
            return true;
        }

        return false;
    }

    private void ClearSenderCopies(IReadOnlyList<StoredMessageLocation> copies,
        long callerUserId, Dictionary<long, List<int>> peerIds)
    {
        foreach (StoredMessageLocation copy in copies)
        {
            if (copy.OwnerId == callerUserId)
            {
                continue;
            }

            byte[] bytes = copy.MessageBytes;
            using var stored = new TLMessage(bytes, 0, bytes.Length);
            if (stored.Type != TLMessage.MessageType.Message)
            {
                continue;
            }
            var message = stored.AsMessage();
            if (!message.OutProperty || !message.MediaUnread)
            {
                continue;
            }

            using TLMessage cleared = message.Clone()
                .MediaUnread(false)
                .Build();
            _messageRepository.PutMessage(copy.OwnerId, cleared, copy.Pts);
            if (!peerIds.TryGetValue(copy.OwnerId, out List<int>? ids))
            {
                ids = new List<int>();
                peerIds[copy.OwnerId] = ids;
            }
            ids.Add(copy.MessageId);
        }
    }

    private static List<int> ReadRequestedIds(TLBytes q)
    {
        var request = (MessagesReadMessageContents)q;
        VectorOfInt ids = request.Id;
        var requested = new List<int>(ids.Count);
        var seen = new HashSet<int>();
        for (int i = 0; i < ids.Count; i++)
        {
            int messageId = ids[i];
            if (messageId > 0 && seen.Add(messageId))
            {
                requested.Add(messageId);
            }
        }
        return requested;
    }

    private static TLUpdate BuildUpdate(IReadOnlyList<int> messageIds, int pts,
        int date)
    {
        var ids = new VectorOfInt();
        foreach (int messageId in messageIds)
        {
            ids.Append(messageId);
        }
        return UpdateReadMessagesContents.Builder()
            .Messages(ids)
            .Pts(pts)
            .PtsCount(1)
            .Date(date)
            .Build();
    }

    private static TLAffectedMessages Error(string message) =>
        (TLAffectedMessages)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));
}
