// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services;

/// <summary>
/// The durable scheduled-message queue and its claim state machine, shared by the
/// send paths, `messages.editMessage`, the three `*ScheduledMessages` handlers and
/// the due coordinator.
///
/// A queue entry stores the fully prepared `message` row, already carrying the
/// scheduled id as its id and the send date as its date, because that is exactly
/// what `updateNewScheduledMessage` must report and what the flush sends.
/// </summary>
public sealed class ScheduledMessageStore
{
    private readonly IScheduledMessagesRepository _scheduledMessagesRepository;

    // A schedule date this close to now is not scheduling at all: Telegram sends it
    // immediately (/api/scheduled-messages) and pinned TDLib does not even transmit
    // one, rewriting any date at or below now + 10 to 0
    // (`MessagesManager.cpp:19726`).
    public const int MinLeadSeconds = 10;

    /// <summary>
    /// The reserved "send when the recipient comes online" date. Pinned TDLib maps
    /// it back to `messageSchedulingStateSendWhenOnline`
    /// (`MessagesManager.h:1645`, `MessagesManager.cpp:19776`), so a row stored
    /// with this date must be flushed by a status change and never by its date.
    /// </summary>
    public const int WhenOnlineDate = 2147483646;

    /// <summary>
    /// Telegram refuses a schedule date more than 367 days out
    /// (`MessagesManager.cpp:19729`).
    /// </summary>
    public const int MaxLeadSeconds = 367 * 86400;

    /// <summary>
    /// A scheduled id must stay a valid `ScheduledServerMessageId`
    /// (`ScheduledServerMessageId.h`: `id > 0 && id < (1 << 18)`), or the pinned
    /// client discards the whole queue entry with a logged error.
    /// </summary>
    public const int MaxScheduledId = (1 << 18) - 1;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICounterFactory _counterFactory;
    private readonly TimeProvider _timeProvider;
    // Claims are compare-and-set over a store with no CAS primitive, so the
    // read/verify/write is serialized here. This is the same single-process
    // guarantee the group-call recording generations rely on; a multi-node
    // deployment would need the claim pushed into the store itself.
    private readonly SemaphoreSlim _claimGate = new(1, 1);

    public ScheduledMessageStore(IUnitOfWork unitOfWork, IScheduledMessagesRepository scheduledMessagesRepository,
        ICounterFactory counterFactory, TimeProvider timeProvider)
    {
        _scheduledMessagesRepository = scheduledMessagesRepository;

        _unitOfWork = unitOfWork;
        _counterFactory = counterFactory;
        _timeProvider = timeProvider;
    }

    /// One queue entry read into heap values, so a caller can await freely.
    public readonly record struct ScheduledSnapshot(long OwnerUserId,
        TLPeer.PeerType PeerType, long PeerId, int ScheduledId, long RandomId,
        int SendDate, int State, int Generation, int ClaimedAt, byte[] MessageBytes,
        int Date)
    {
        public bool SendsWhenOnline => SendDate == WhenOnlineDate;
    }

    // ---- classifying a request ------------------------------------------

    /// <summary>
    /// Whether a `schedule_date` addresses the queue at all. A date at or below the
    /// ten-second lead is an ordinary immediate send, which is what the pinned
    /// client and the documented API both do with it.
    /// </summary>
    public static bool IsQueued(int scheduleDate, int now) =>
        scheduleDate == WhenOnlineDate ||
        (scheduleDate > 0 && scheduleDate - now >= MinLeadSeconds);

    /// <summary>
    /// Rejects a schedule date the protocol does not permit. `when online` is only
    /// meaningful for another user's private dialog: there is nobody else's status
    /// to wait for in a group, a channel or Saved Messages
    /// (`MessagesManager.cpp:21107-21114`).
    /// </summary>
    public static ErrorMessage? ValidateScheduleDate(int scheduleDate, int now,
        TLPeer.PeerType peerType, long peerId, long selfUserId)
    {
        if (scheduleDate == WhenOnlineDate)
        {
            return peerType == TLPeer.PeerType.PeerUser && peerId != selfUserId
                ? null
                : new ErrorMessage(400, "SCHEDULE_STATUS_PRIVATE");
        }
        return scheduleDate - now > MaxLeadSeconds
            ? new ErrorMessage(400, "SCHEDULE_DATE_TOO_LATE")
            : null;
    }

    // ---- enqueueing -----------------------------------------------------

    /// <summary>
    /// Allocates this dialog's next scheduled id, stamps it plus the send date onto
    /// the prepared row, and stores the entry. The unit of work is committed by the
    /// caller.
    /// </summary>
    public async Task<ScheduledSnapshot?> EnqueueAsync(long ownerUserId,
        TLPeer.PeerType peerType, long peerId, long randomId, int sendDate,
        byte[] templateBytes)
    {
        int scheduledId = (int)await NextScheduledIdAsync(ownerUserId, peerType,
            peerId);
        if (scheduledId <= 0 || scheduledId > MaxScheduledId)
        {
            // Past 2^18 the pinned client cannot represent the id at all, so the
            // truthful answer is that this dialog's queue is full rather than a
            // row the client will silently drop.
            return null;
        }

        byte[] rowBytes = StampScheduled(templateBytes, scheduledId, sendDate);
        var snapshot = new ScheduledSnapshot(ownerUserId, peerType, peerId,
            scheduledId, randomId, sendDate, ScheduledMessageState.Queued,
            Generation: 0, ClaimedAt: 0, rowBytes, UnixNow());
        return Put(snapshot) ? snapshot : null;
    }

    // ---- reading --------------------------------------------------------

    public async ValueTask<ScheduledSnapshot?> GetAsync(long ownerUserId,
        TLPeer.PeerType peerType, long peerId, int scheduledId)
    {
        using TLScheduledMessage? row = await _scheduledMessagesRepository
            .GetScheduledMessageAsync(ownerUserId, (int)peerType, peerId, scheduledId);
        return row == null ? null : ReadSnapshot(row.Value);
    }

    /// One dialog's queue, oldest scheduled id first, which is the stable order
    /// `getScheduledHistory` and `getScheduledMessages` both answer in.
    public async ValueTask<IReadOnlyList<ScheduledSnapshot>> GetQueueAsync(
        long ownerUserId, TLPeer.PeerType peerType, long peerId)
    {
        IReadOnlyCollection<TLScheduledMessage> rows = await _scheduledMessagesRepository.GetScheduledMessagesAsync(ownerUserId,
                (int)peerType, peerId);
        return ReadSnapshots(rows).OrderBy(x => x.ScheduledId).ToArray();
    }

    public async ValueTask<IReadOnlyList<ScheduledSnapshot>> GetAllAsync()
    {
        IReadOnlyCollection<TLScheduledMessage> rows = await _scheduledMessagesRepository.GetAllScheduledMessagesAsync();
        return ReadSnapshots(rows)
            .OrderBy(x => x.SendDate)
            .ThenBy(x => x.OwnerUserId)
            .ThenBy(x => x.ScheduledId)
            .ToArray();
    }

    /// <summary>
    /// Entries whose date has arrived. `when online` rows are excluded on purpose:
    /// their date is a sentinel in 2038, not a deadline.
    /// </summary>
    public async ValueTask<IReadOnlyList<ScheduledSnapshot>> GetDueAsync(int now)
    {
        IReadOnlyList<ScheduledSnapshot> all = await GetAllAsync();
        return all.Where(x => x.State == ScheduledMessageState.Queued &&
                              !x.SendsWhenOnline && x.SendDate <= now).ToArray();
    }

    /// Entries waiting for one user to come online in their private dialog.
    public async ValueTask<IReadOnlyList<ScheduledSnapshot>> GetWhenOnlineAsync(
        long recipientUserId)
    {
        IReadOnlyList<ScheduledSnapshot> all = await GetAllAsync();
        return all.Where(x => x.State == ScheduledMessageState.Queued &&
                              x.SendsWhenOnline &&
                              x.PeerType == TLPeer.PeerType.PeerUser &&
                              x.PeerId == recipientUserId).ToArray();
    }

    /// Entries a previous process claimed and never finished flushing.
    public async ValueTask<IReadOnlyList<ScheduledSnapshot>> GetAbandonedClaimsAsync()
    {
        IReadOnlyList<ScheduledSnapshot> all = await GetAllAsync();
        return all.Where(x => x.State == ScheduledMessageState.Claimed).ToArray();
    }

    // ---- claiming, rescheduling, deleting -------------------------------

    /// <summary>
    /// Takes exclusive ownership of one entry for a flush. Re-reads the row so a
    /// caller holding a stale snapshot loses, which is what makes a timer tick, a
    /// manual `messages.sendScheduledMessages` and a restart unable to send the
    /// same entry twice. The write is committed here, because a claim is only
    /// worth anything once it is durable.
    /// </summary>
    public async Task<ScheduledSnapshot?> TryClaimAsync(ScheduledSnapshot requested)
    {
        await _claimGate.WaitAsync();
        try
        {
            ScheduledSnapshot? current = await GetAsync(requested.OwnerUserId,
                requested.PeerType, requested.PeerId, requested.ScheduledId);
            if (current is not { State: ScheduledMessageState.Queued } ||
                current.Value.Generation != requested.Generation)
            {
                return null;
            }

            ScheduledSnapshot claimed = current.Value with
            {
                State = ScheduledMessageState.Claimed,
                Generation = current.Value.Generation + 1,
                ClaimedAt = UnixNow(),
            };
            return Put(claimed) && await _unitOfWork.SaveAsync() ? claimed : null;
        }
        finally
        {
            _claimGate.Release();
        }
    }

    /// <summary>
    /// Returns a claimed entry to the queue when the flush it was claimed for did
    /// not send anything. The generation stays bumped, so the losing claimant still
    /// cannot act on its stale snapshot; what is restored is the entry, not the
    /// claim. Without this a flush that fails its rights re-check would leave a row
    /// the client can still see but nothing can ever send.
    /// </summary>
    public async Task<bool> ReleaseClaimAsync(ScheduledSnapshot claimed)
    {
        await _claimGate.WaitAsync();
        try
        {
            ScheduledSnapshot released = claimed with
            {
                State = ScheduledMessageState.Queued,
                ClaimedAt = 0,
            };
            return Put(released) && await _unitOfWork.SaveAsync();
        }
        finally
        {
            _claimGate.Release();
        }
    }

    /// <summary>
    /// Moves an entry to a new send date, keeping its scheduled id so the client's
    /// queue entry is updated rather than replaced (/api/scheduled-messages).
    /// </summary>
    public ScheduledSnapshot? Reschedule(ScheduledSnapshot snapshot, int sendDate)
    {
        ScheduledSnapshot moved = snapshot with
        {
            SendDate = sendDate,
            MessageBytes = StampScheduled(snapshot.MessageBytes,
                snapshot.ScheduledId, sendDate),
        };
        return Put(moved) ? moved : null;
    }

    /// Replaces an entry's prepared row, keeping its scheduled id and send date.
    public ScheduledSnapshot? ReplaceContent(ScheduledSnapshot snapshot,
        byte[] messageBytes)
    {
        ScheduledSnapshot edited = snapshot with
        {
            MessageBytes = StampScheduled(messageBytes, snapshot.ScheduledId,
                snapshot.SendDate),
        };
        return Put(edited) ? edited : null;
    }

    public bool Delete(ScheduledSnapshot snapshot) =>
        _scheduledMessagesRepository.DeleteScheduledMessage(
            snapshot.OwnerUserId, (int)snapshot.PeerType, snapshot.PeerId,
            snapshot.ScheduledId);

    public bool Put(ScheduledSnapshot snapshot)
    {
        var builder = ScheduledMessage.Builder()
            .OwnerUserId(snapshot.OwnerUserId)
            .PeerType((int)snapshot.PeerType)
            .PeerId(snapshot.PeerId)
            .ScheduledId(snapshot.ScheduledId)
            .RandomId(snapshot.RandomId)
            .SendDate(snapshot.SendDate)
            .State(snapshot.State)
            .Generation(snapshot.Generation)
            .Message(snapshot.MessageBytes)
            .Date(snapshot.Date);
        if (snapshot.ClaimedAt > 0)
        {
            builder = builder.ClaimedAt(snapshot.ClaimedAt);
        }
        using TLScheduledMessage row = builder.Build();
        return _scheduledMessagesRepository.PutScheduledMessage(row);
    }

    // ---- wire values ----------------------------------------------------

    /// <summary>
    /// The `updateNewScheduledMessage` a client receives when an entry is created
    /// or changed. It carries the queue row verbatim, so its id is the scheduled id
    /// and its date is the send date.
    /// </summary>
    public static TLUpdate BuildNewScheduledUpdate(ScheduledSnapshot snapshot) =>
        UpdateNewScheduledMessage.Builder()
            .Message(snapshot.MessageBytes)
            .Build();

    /// <summary>
    /// The `updateDeleteScheduledMessages` that reports entries leaving the queue.
    /// `sent_messages` is index-aligned with `messages`, so a flush pairs each
    /// scheduled id with the real id it became and a delete omits the field
    /// entirely.
    /// </summary>
    public static TLUpdate BuildDeleteScheduledUpdate(TLPeer.PeerType peerType,
        long peerId, IReadOnlyList<int> scheduledIds,
        IReadOnlyList<int>? sentIds = null)
    {
        using TLPeer peer = PeerResolver.BuildPeer(peerType, peerId);
        var messages = new VectorOfInt();
        foreach (int id in scheduledIds)
        {
            messages.Append(id);
        }
        var builder = UpdateDeleteScheduledMessages.Builder()
            .Peer(peer.AsSpan())
            .Messages(messages);
        if (sentIds != null)
        {
            var sent = new VectorOfInt();
            foreach (int id in sentIds)
            {
                sent.Append(id);
            }
            builder = builder.SentMessages(sent);
        }
        return builder.Build();
    }

    public int UnixNow() =>
        checked((int)_timeProvider.GetUtcNow().ToUnixTimeSeconds());

    // ---- plumbing -------------------------------------------------------

    /// <summary>
    /// Rewrites the prepared row's id and date. Both are non-optional fields of
    /// `message`, so a builder clone expresses them directly.
    /// </summary>
    private static byte[] StampScheduled(byte[] messageBytes, int scheduledId,
        int sendDate)
    {
        using var stored = new TLMessage(messageBytes, 0, messageBytes.Length);
        using TLMessage stamped = stored.AsMessage().Clone()
            .Id(scheduledId)
            .Date(sendDate)
            .Build();
        return stamped.AsSpan().ToArray();
    }

    // Each dialog has its own scheduled id sequence, per /api/scheduled-messages,
    // and the sequence belongs to the scheduling user because a queue is only ever
    // read back by its own owner.
    private ValueTask<long> NextScheduledIdAsync(long ownerUserId,
        TLPeer.PeerType peerType, long peerId) =>
        _counterFactory.GetCounter(
                $"counter_scheduled_message_id_{ownerUserId}_{(int)peerType}_{peerId}")
            .IncrementAndGet();

    private static IEnumerable<ScheduledSnapshot> ReadSnapshots(
        IReadOnlyCollection<TLScheduledMessage> rows)
    {
        var snapshots = new List<ScheduledSnapshot>(rows.Count);
        foreach (TLScheduledMessage row in rows)
        {
            using TLScheduledMessage owned = row;
            snapshots.Add(ReadSnapshot(owned));
        }
        return snapshots;
    }

    private static ScheduledSnapshot ReadSnapshot(TLScheduledMessage row)
    {
        var body = row.AsScheduledMessage();
        return new ScheduledSnapshot(body.OwnerUserId,
            (TLPeer.PeerType)body.PeerType, body.PeerId, body.ScheduledId,
            body.RandomId, body.SendDate, body.State, body.Generation,
            body.Flags[0] ? body.ClaimedAt : 0, body.Message.ToArray(), body.Date);
    }
}
