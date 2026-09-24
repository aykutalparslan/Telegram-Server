// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Services.Transport;
using Ferrite.Utils;

namespace Ferrite.Core.Connection;

public sealed class SessionOutbox : ISessionOutbox
{
    public const int MessagesPerSession = 256;
    public const int RetentionSeconds = 300;
    public const int SweepIntervalSeconds = 30;

    private readonly IMTProtoTime _time;
    private readonly object _lock = new();
    private readonly Dictionary<(long AuthKeyId, long SessionId), List<Entry>> _bySession = new();
    private long _lastSweep;

    public SessionOutbox(IMTProtoTime time)
    {
        _time = time;
    }

    public void Track(long authKeyId, long sessionId, object owner, MTProtoMessage original,
        MTProtoMessage sent)
    {
        long now = _time.GetUnixTimeInSeconds();
        lock (_lock)
        {
            SweepIfDue(now);
            var key = (authKeyId, sessionId);
            if (!_bySession.TryGetValue(key, out var entries))
            {
                entries = new List<Entry>();
                _bySession[key] = entries;
            }

            if (entries.Count >= MessagesPerSession)
            {
                entries.RemoveRange(0, entries.Count - MessagesPerSession + 1);
            }

            entries.Add(new Entry(owner, original, sent, now));
        }
    }

    public void MarkSent(long authKeyId, long sessionId, MTProtoMessage sent, long messageId)
    {
        lock (_lock)
        {
            if (!_bySession.TryGetValue((authKeyId, sessionId), out var entries))
            {
                return;
            }

            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(entries[i].Sent, sent))
                {
                    entries[i].MessageId = messageId;
                    return;
                }
            }
        }
    }

    public void Acknowledge(long authKeyId, long sessionId, long messageId)
    {
        if (messageId == 0)
        {
            return;
        }

        lock (_lock)
        {
            var key = (authKeyId, sessionId);
            if (!_bySession.TryGetValue(key, out var entries))
            {
                return;
            }

            entries.RemoveAll(entry => entry.MessageId == messageId);
            if (entries.Count == 0)
            {
                _bySession.Remove(key);
            }
        }
    }

    public IReadOnlyList<MTProtoMessage> TakeFromOtherOwners(long authKeyId, long sessionId,
        object owner)
    {
        long now = _time.GetUnixTimeInSeconds();
        lock (_lock)
        {
            var key = (authKeyId, sessionId);
            if (!_bySession.TryGetValue(key, out var entries))
            {
                return Array.Empty<MTProtoMessage>();
            }

            var taken = new List<MTProtoMessage>();
            entries.RemoveAll(entry =>
            {
                if (IsExpired(entry, now))
                {
                    return true;
                }
                if (ReferenceEquals(entry.Owner, owner))
                {
                    return false;
                }
                taken.Add(entry.Original);
                return true;
            });
            if (entries.Count == 0)
            {
                _bySession.Remove(key);
            }
            return taken;
        }
    }

    private void SweepIfDue(long now)
    {
        if (now - _lastSweep < SweepIntervalSeconds)
        {
            return;
        }

        _lastSweep = now;
        List<(long, long)>? empty = null;
        foreach (var (key, entries) in _bySession)
        {
            entries.RemoveAll(entry => IsExpired(entry, now));
            if (entries.Count == 0)
            {
                (empty ??= new()).Add(key);
            }
        }

        if (empty != null)
        {
            foreach (var key in empty)
            {
                _bySession.Remove(key);
            }
        }
    }

    private static bool IsExpired(Entry entry, long now) =>
        now - entry.TrackedAt >= RetentionSeconds;

    private sealed class Entry
    {
        public Entry(object owner, MTProtoMessage original, MTProtoMessage sent, long trackedAt)
        {
            Owner = owner;
            Original = original;
            Sent = sent;
            TrackedAt = trackedAt;
        }

        public object Owner { get; }
        public MTProtoMessage Original { get; }
        public MTProtoMessage Sent { get; }
        public long TrackedAt { get; }
        public long MessageId { get; set; }
    }
}
