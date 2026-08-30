// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Scheduling;

public sealed class ScheduledMessageStore
{
    private readonly IScheduledMessagesRepository _scheduledMessagesRepository;

    public const int MinLeadSeconds = 10;

    public const int WhenOnlineDate = 2147483646;

    public const int MaxLeadSeconds = 367 * 86400;

    public const int MaxScheduledId = (1 << 18) - 1;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICounterFactory _counterFactory;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _claimGate = new(1, 1);

    public ScheduledMessageStore(IUnitOfWork unitOfWork, IScheduledMessagesRepository scheduledMessagesRepository,
        ICounterFactory counterFactory, TimeProvider timeProvider)
    {
        _scheduledMessagesRepository = scheduledMessagesRepository;

        _unitOfWork = unitOfWork;
        _counterFactory = counterFactory;
        _timeProvider = timeProvider;
    }

    public readonly record struct ScheduledSnapshot(long OwnerUserId,
        TLPeer.PeerType PeerType, long PeerId, int ScheduledId, long RandomId,
        int SendDate, int State, int Generation, int ClaimedAt, byte[] MessageBytes,
        int Date)
    {
        public bool SendsWhenOnline => SendDate == WhenOnlineDate;
    }

    public static bool IsQueued(int scheduleDate, int now) =>
        scheduleDate == WhenOnlineDate ||
        (scheduleDate > 0 && scheduleDate - now >= MinLeadSeconds);

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

    public async Task<ScheduledSnapshot?> EnqueueAsync(long ownerUserId,
        TLPeer.PeerType peerType, long peerId, long randomId, int sendDate,
        byte[] templateBytes)
    {
        int scheduledId = (int)await NextScheduledIdAsync(ownerUserId, peerType,
            peerId);
        if (scheduledId <= 0 || scheduledId > MaxScheduledId)
        {
            return null;
        }

        byte[] rowBytes = StampScheduled(templateBytes, scheduledId, sendDate);
        var snapshot = new ScheduledSnapshot(ownerUserId, peerType, peerId,
            scheduledId, randomId, sendDate, ScheduledMessageState.Queued,
            Generation: 0, ClaimedAt: 0, rowBytes, UnixNow());
        return Put(snapshot) ? snapshot : null;
    }

    public async ValueTask<ScheduledSnapshot?> GetAsync(long ownerUserId,
        TLPeer.PeerType peerType, long peerId, int scheduledId)
    {
        using TLScheduledMessage? row = await _scheduledMessagesRepository
            .GetScheduledMessageAsync(ownerUserId, (int)peerType, peerId, scheduledId);
        return row == null ? null : ReadSnapshot(row.Value);
    }

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

    public async ValueTask<IReadOnlyList<ScheduledSnapshot>> GetDueAsync(int now)
    {
        IReadOnlyList<ScheduledSnapshot> all = await GetAllAsync();
        return all.Where(x => x.State == ScheduledMessageState.Queued &&
                              !x.SendsWhenOnline && x.SendDate <= now).ToArray();
    }

    public async ValueTask<IReadOnlyList<ScheduledSnapshot>> GetWhenOnlineAsync(
        long recipientUserId)
    {
        IReadOnlyList<ScheduledSnapshot> all = await GetAllAsync();
        return all.Where(x => x.State == ScheduledMessageState.Queued &&
                              x.SendsWhenOnline &&
                              x.PeerType == TLPeer.PeerType.PeerUser &&
                              x.PeerId == recipientUserId).ToArray();
    }

    public async ValueTask<IReadOnlyList<ScheduledSnapshot>> GetAbandonedClaimsAsync()
    {
        IReadOnlyList<ScheduledSnapshot> all = await GetAllAsync();
        return all.Where(x => x.State == ScheduledMessageState.Claimed).ToArray();
    }

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

    public static TLUpdate BuildNewScheduledUpdate(ScheduledSnapshot snapshot) =>
        UpdateNewScheduledMessage.Builder()
            .Message(snapshot.MessageBytes)
            .Build();

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
