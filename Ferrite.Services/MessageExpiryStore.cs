// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services;

/// <summary>
/// The auto-delete side of a conversation's history TTL: what `ttl_period` a new
/// message inherits, and one durable expiry row per stored copy that carries one.
/// The index is the whole of the promise, so a stamped period is never written
/// without it.
/// </summary>
public sealed class MessageExpiryStore
{
    private readonly IExpiringMessagesRepository _expiringMessagesRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ChatSettingsStore _settings;
    private readonly TimeProvider _timeProvider;

    public MessageExpiryStore(IUnitOfWork unitOfWork, IExpiringMessagesRepository expiringMessagesRepository, ChatSettingsStore settings,
        TimeProvider timeProvider)
    {
        _expiringMessagesRepository = expiringMessagesRepository;

        _unitOfWork = unitOfWork;
        _settings = settings;
        _timeProvider = timeProvider;
    }

    public readonly record struct ExpirySnapshot(int BoxType, long BoxId,
        int MessageId, int ExpiresAt, int Date);

    public static ChatSettingsScope? ResolveScope(long selfUserId,
        TLPeer.PeerType peerType, long peerId) => peerType switch
    {
        TLPeer.PeerType.PeerUser => ChatSettingsScope.ForPrivatePair(selfUserId,
            peerId),
        TLPeer.PeerType.PeerChat => ChatSettingsScope.ForChat(peerId),
        TLPeer.PeerType.PeerChannel => ChatSettingsScope.ForChannel(peerId),
        _ => null
    };

    public async ValueTask<int> ResolveTtlPeriodAsync(long selfUserId,
        TLPeer.PeerType peerType, long peerId)
    {
        if (ResolveScope(selfUserId, peerType, peerId) is not { } scope)
        {
            return 0;
        }
        ChatSettingsSnapshot snapshot = await _settings.GetAsync(scope);
        return snapshot.TtlPeriod > 0 ? snapshot.TtlPeriod : 0;
    }

    /// <summary>
    /// Records that one stored copy is due at `date + ttlPeriod`. The caller owns
    /// the commit, so the row lands with the message it describes.
    /// </summary>
    public bool Track(int boxType, long boxId, int messageId, int date,
        int ttlPeriod)
    {
        if (ttlPeriod <= 0)
        {
            return false;
        }
        using TLExpiringMessage row = ExpiringMessage.Builder()
            .BoxType(boxType)
            .BoxId(boxId)
            .MessageId(messageId)
            .ExpiresAt(date + ttlPeriod)
            .Date(date)
            .Build();
        return _expiringMessagesRepository.PutExpiringMessage(row);
    }

    public bool Untrack(int boxType, long boxId, int messageId) =>
        _expiringMessagesRepository.DeleteExpiringMessage(boxType, boxId,
            messageId);

    public async ValueTask<ExpirySnapshot?> GetAsync(int boxType, long boxId,
        int messageId)
    {
        using TLExpiringMessage? stored = await _expiringMessagesRepository.GetExpiringMessageAsync(boxType, boxId,
                messageId);
        return stored == null ? null : ToSnapshot(stored.Value);
    }

    /// Every copy whose expiry has arrived, oldest first.
    public async ValueTask<IReadOnlyList<ExpirySnapshot>> GetDueAsync(int now)
    {
        var due = new List<ExpirySnapshot>();
        IReadOnlyCollection<TLExpiringMessage> rows = await _expiringMessagesRepository.GetAllExpiringMessagesAsync();
        foreach (TLExpiringMessage row in rows)
        {
            using TLExpiringMessage owned = row;
            ExpirySnapshot snapshot = ToSnapshot(owned);
            if (snapshot.ExpiresAt <= now)
            {
                due.Add(snapshot);
            }
        }
        due.Sort(static (left, right) => left.ExpiresAt.CompareTo(right.ExpiresAt));
        return due;
    }

    public int UnixNow() =>
        checked((int)_timeProvider.GetUtcNow().ToUnixTimeSeconds());

    private static ExpirySnapshot ToSnapshot(TLExpiringMessage row)
    {
        var value = row.AsExpiringMessage();
        return new ExpirySnapshot(value.BoxType, value.BoxId, value.MessageId,
            value.ExpiresAt, value.Date);
    }
}
