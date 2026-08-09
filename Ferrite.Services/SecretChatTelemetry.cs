// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Diagnostics.Metrics;
using Ferrite.Utils;

namespace Ferrite.Services;

/// <summary>
/// Redacted secret-chat diagnostics. Only identifiers, states, counts and byte
/// sizes cross this boundary; encrypted payloads and secret material never do.
/// </summary>
public sealed class SecretChatTelemetry : IDisposable
{
    private readonly ILogger? _log;
    private readonly Meter _meter = new("Ferrite.SecretChats", "1.0.0");
    private readonly Counter<long> _transitions;
    private readonly Counter<long> _qtsAppends;
    private readonly Counter<long> _serviceRelays;
    private readonly Counter<long> _rejections;
    private readonly Counter<long> _cleanupRuns;
    private readonly Histogram<long> _ciphertextBytes;
    private readonly Histogram<long> _queueBytes;
    private long _transitionCount;
    private long _qtsAppendCount;
    private long _serviceRelayCount;
    private long _rejectionCount;
    private long _cleanupCount;

    public SecretChatTelemetry(ILogger? log = null)
    {
        _log = log;
        _transitions = _meter.CreateCounter<long>(
            "ferrite.secret_chat.transitions");
        _qtsAppends = _meter.CreateCounter<long>(
            "ferrite.secret_chat.qts_appends");
        _serviceRelays = _meter.CreateCounter<long>(
            "ferrite.secret_chat.service_relays");
        _rejections = _meter.CreateCounter<long>(
            "ferrite.secret_chat.rejections");
        _cleanupRuns = _meter.CreateCounter<long>(
            "ferrite.secret_chat.cleanup_runs");
        _ciphertextBytes = _meter.CreateHistogram<long>(
            "ferrite.secret_chat.ciphertext_bytes", "By");
        _queueBytes = _meter.CreateHistogram<long>(
            "ferrite.secret_chat.queue_bytes", "By");
    }

    public long TransitionCount => Interlocked.Read(ref _transitionCount);
    public long QtsAppendCount => Interlocked.Read(ref _qtsAppendCount);
    public long ServiceRelayCount => Interlocked.Read(ref _serviceRelayCount);
    public long RejectionCount => Interlocked.Read(ref _rejectionCount);
    public long CleanupCount => Interlocked.Read(ref _cleanupCount);

    public void Transition(long authKeyId, int chatId, string state)
    {
        Interlocked.Increment(ref _transitionCount);
        _transitions.Add(1, new KeyValuePair<string, object?>("state", state));
        _log?.Information($"secret_chat_transition auth_key_id={authKeyId} " +
                          $"chat_id={chatId} state={state}");
    }

    public void QtsAppend(long authKeyId, int chatId, int qts,
        long encryptedBytes)
    {
        Interlocked.Increment(ref _qtsAppendCount);
        _qtsAppends.Add(1);
        _ciphertextBytes.Record(encryptedBytes);
        _log?.Debug($"secret_chat_qts_append auth_key_id={authKeyId} " +
                    $"chat_id={chatId} qts={qts} ciphertext_bytes={encryptedBytes}");
    }

    public void ServiceRelay(long authKeyId, int chatId, long encryptedBytes)
    {
        Interlocked.Increment(ref _serviceRelayCount);
        _serviceRelays.Add(1);
        _ciphertextBytes.Record(encryptedBytes,
            new KeyValuePair<string, object?>("operation", "service_relay"));
        _log?.Debug($"secret_chat_service_relay auth_key_id={authKeyId} " +
                    $"chat_id={chatId} ciphertext_bytes={encryptedBytes}");
    }

    public void Rejection(string operation, long authKeyId, int chatId,
        string reason)
    {
        Interlocked.Increment(ref _rejectionCount);
        _rejections.Add(1,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("reason", reason));
        _log?.Information($"secret_chat_rejection operation={operation} " +
                          $"auth_key_id={authKeyId} chat_id={chatId} reason={reason}");
    }

    public void Cleanup(int authKeys, int recoveredPending, int expiredEvents,
        long expiredBytes, int deletedReceipts, int deletedControls,
        int queuedEvents, long queuedBytes)
    {
        Interlocked.Increment(ref _cleanupCount);
        _cleanupRuns.Add(1);
        _ciphertextBytes.Record(expiredBytes,
            new KeyValuePair<string, object?>("operation", "expired"));
        _queueBytes.Record(queuedBytes);
        _log?.Information($"secret_chat_cleanup auth_keys={authKeys} " +
                          $"recovered_pending={recoveredPending} " +
                          $"expired_events={expiredEvents} expired_bytes={expiredBytes} " +
                          $"deleted_receipts={deletedReceipts} " +
                          $"deleted_controls={deletedControls} " +
                          $"queued_events={queuedEvents} queued_bytes={queuedBytes}");
    }

    public void Dispose() => _meter.Dispose();
}
