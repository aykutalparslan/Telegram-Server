// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Buffers;
using System.Net;
using System.Security.Cryptography;
using Ferrite.Crypto;
using Ferrite.Data;
using Ferrite.Services;
using Ferrite.TL.mtproto;
using Ferrite.Utils;

namespace Ferrite.Core.Connection;

public class MTProtoSession : IMTProtoSession
{
    public const int SentMessageRegistryCapacity = 1024;
    private readonly IMTProtoService _mtproto;
    private readonly ILogger _log;
    private readonly IMTProtoTime _time;
    private readonly ISessionService _sessionService;
    private readonly IRandomGenerator _random;
    public MTProtoConnection? Connection { get; set; }
    public IPEndPoint? EndPoint { get; set; }
    private long _authKeyId;
    private long _permAuthKeyId;
    private byte[]? _authKey;
    private long _sessionId;
    private long _uniqueSessionId;
    private long _serverSalt;
    private int _serverSaltValidUntil;
    private bool _serverSaltInitialized;
    private int _seq = 0;
    private long _lastMessageId;
    private readonly CircularQueue<long> _lastMessageIds = new CircularQueue<long>(10);
    private readonly CircularQueue<long> _receivedMessageIds = new CircularQueue<long>(10);
    private readonly object _sentMessagesLock = new();
    private readonly Dictionary<long, MTProtoSentMessage> _sentMessages = new();
    private readonly Queue<long> _sentMessageOrder = new();
    private Dictionary<string, object> _sessionData = new();

    public MTProtoSession(IMTProtoService mtproto, ILogger log,
        IMTProtoTime time, ISessionService sessionService, IRandomGenerator random)
    {
        _mtproto = mtproto;
        _log = log;
        _time = time;
        _sessionService = sessionService;
        _random = random;
    }
    
    public long AuthKeyId => _authKeyId;
    public long PermAuthKeyId => _permAuthKeyId;
    public virtual byte[]? AuthKey => _authKey;
    public long SessionId => _sessionId;
    public long UniqueSessionId => _uniqueSessionId;
    public long ServerSalt => _serverSalt;
    public Dictionary<string, object> SessionData => _sessionData;

    public bool TryFetchAuthKey(long authKeyId)
    {
        if (Interlocked.CompareExchange(
                ref _authKeyId,
                authKeyId,
                0)
            != 0) return false;
        
        var authKey = _mtproto.GetAuthKey(_authKeyId);
        if (authKey != null)
        {
            _permAuthKeyId = _authKeyId;
            _log.Information($"Fetched the authKey with Id: {_authKeyId}");
        }
        else
        {
            authKey = _mtproto.GetTempAuthKey(_authKeyId);
            TryGetPermAuthKeyId();
            _log.Information($"Fetched the tempAuthKey with Id: {_authKeyId}");
        }

        if (authKey is { Length: 192 })
        {
            _authKey = authKey;
        }
        else
        {
            _authKeyId = 0;
            _permAuthKeyId = 0;
        }

        return _authKey != null;
    }

    private bool TryGetPermAuthKeyId()
    {
        if (_authKeyId == 0 || _permAuthKeyId != 0) return false;
        var pKey = _mtproto.GetBoundAuthKey(_authKeyId);
        _permAuthKeyId = pKey ?? 0;
        if (_permAuthKeyId != 0)
        {
            _log.Information($"Retrieved the permAuthKeyId: {_permAuthKeyId}");
        }
        return _permAuthKeyId != 0;
    }

    public int GenerateQuickAck(Span<byte> messageSpan)
    {
        var sha256 = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        sha256.AppendData(_authKey.AsSpan().Slice(88, 32));
        sha256.AppendData(messageSpan);
        var ack = sha256.GetCurrentHash();
        return BitConverter.ToInt32(ack, 0);
    }

    public int GenerateSeqNo(bool isContentRelated)
    {
        return isContentRelated ? (2 * _seq++) + 1 : 2 * _seq;
    }

    public void RecordSentMessage(long messageId, int sequenceNo, int length, bool contentRelated)
    {
        RecordSentMessage(messageId, sequenceNo, length, contentRelated, responseToMessageId: 0);
    }

    public void RecordSentMessage(long messageId, int sequenceNo, int length, bool contentRelated,
        long responseToMessageId)
    {
        lock (_sentMessagesLock)
        {
            if (!_sentMessages.ContainsKey(messageId))
            {
                while (_sentMessages.Count >= SentMessageRegistryCapacity &&
                       _sentMessageOrder.TryDequeue(out var evicted))
                {
                    _sentMessages.Remove(evicted);
                }

                _sentMessageOrder.Enqueue(messageId);
            }

            _sentMessages[messageId] = new MTProtoSentMessage(
                messageId,
                MTProtoMessageStatus.ForSentMessage(contentRelated),
                sequenceNo,
                length,
                contentRelated,
                responseToMessageId);
        }
    }

    public bool TryGetSentMessage(long messageId, out MTProtoSentMessage message)
    {
        lock (_sentMessagesLock)
        {
            return _sentMessages.TryGetValue(messageId, out message);
        }
    }

    public bool MarkSentMessageAcknowledged(long messageId)
    {
        lock (_sentMessagesLock)
        {
            if (!_sentMessages.TryGetValue(messageId, out var message))
            {
                return false;
            }

            _sentMessages[messageId] = message with
            {
                Status = MTProtoMessageStatus.Acknowledged(message.Status)
            };
            return true;
        }
    }

    /// <summary>
    /// Gets the next Message Identifier (msg_id) for this session.
    /// </summary>
    /// <param name="response">If the message is a response to a client message.</param>
    /// <returns></returns>
    public long NextMessageId(bool response)
    {
        long id = _time.GetUnixTimeInSeconds();
        id *= 4294967296L;
        long r1 = (4 - id % 4) % 4;
        id += (response ? r1 + 1 : r1 + 3);
        long last = _lastMessageId;
        long r2 = 4 - (last + 1) % 4;
        if (id <= last)
        {
            id = Interlocked.Add(ref _lastMessageId,
                response ? r2 + 2 : r2 + 4);
            if ((response && id % 4 == 1) || (!response && id % 4 == 3))
            {
                return id;
            }
        }
        else if (Interlocked.CompareExchange(ref _lastMessageId, id, last) == last)
        {
            return id;
        }
        do
        {
            r2 = 4 - (_lastMessageId + 1) % 4;
            id = Interlocked.Add(ref _lastMessageId, response ? r2 + 2 : r2 + 4);
        } while (!((response && id % 4 != 1) || (!response && id % 4 != 3)));
        return id;
    }
    public long CreateNewSession(long sessionId, long firstMessageId)
    {
        _sessionId = sessionId;
        _uniqueSessionId = _random.NextLong();
       return SaveCurrentSession(_permAuthKeyId != 0 ? 
                _permAuthKeyId : _authKeyId);
    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="authKeyId"></param>
    /// <returns>Current Server Salt</returns>
    public long SaveCurrentSession(long authKeyId)
    {
        if (_authKeyId != 0 &&
            (!_serverSaltInitialized ||
             _serverSaltValidUntil <= _time.GetUnixTimeInSeconds()))
        {
            RefreshServerSalt();
        }
        
        if (authKeyId != 0)
        {
            if (Connection != null)
                _sessionService.AddSession(authKeyId, _sessionId,
                    new ActiveSession(Connection));
        }
        return _serverSalt;
    }
    /// <summary>
    /// Checks if the given message Id is valid and adds it to the last N messages list
    /// </summary>
    /// <param name="messageId"></param>
    /// <returns></returns>
    public bool IsValidMessageId(long messageId)
    {
        return TryValidateMessageId(messageId, out _);
    }

    public bool TryValidateMessageId(long messageId, out int errorCode,
        bool isContainer = false)
    {
        if (messageId >= _time.ThirtySecondsLater || //msg_id values that belong over 30 seconds in the future
            messageId <= _time.FiveMinutesAgo || //or over 300 seconds in the past are to be ignored
            messageId % 4 != 0 || //client message ids must be divisible by 4
            _receivedMessageIds.Contains(messageId) || //must not be equal to any
            (!isContainer && _lastMessageIds.Count != 0 &&
             messageId <= _lastMessageIds.Min()) //must not be lower than all non-container messages
           )
        {
            errorCode = GetInvalidMessageIdErrorCode(messageId);
            return false;
        }

        _receivedMessageIds.Enqueue(messageId);
        // A container's msg_id is newer than every message it contains. Do not
        // use it as the lower bound: contained messages may later be resent
        // individually or regrouped in a different container.
        if (!isContainer)
        {
            _lastMessageIds.Enqueue(messageId);
        }
        errorCode = 0;
        return true;
    }

    public bool IsValidServerSalt(long serverSalt, out long currentServerSalt)
    {
        if (!_serverSaltInitialized)
        {
            RefreshServerSalt();
        }

        currentServerSalt = _serverSalt;
        return serverSalt == currentServerSalt;
    }

    private void RefreshServerSalt()
    {
        var salts = _mtproto.GetServerSalts(_authKeyId, 1);
        var now = checked((int)_time.GetUnixTimeInSeconds());
        long? selectedSalt = null;
        int selectedValidUntil = 0;

        foreach (TLFutureSalt salt in salts)
        {
            using (salt)
            {
                var value = salt.AsFutureSalt();
                if (selectedSalt == null ||
                    value.ValidSince <= now && value.ValidUntil > now)
                {
                    selectedSalt = value.Salt;
                    selectedValidUntil = value.ValidUntil;
                }
            }
        }

        if (selectedSalt == null)
        {
            throw new InvalidOperationException(
                $"No server salt is available for auth key {_authKeyId}.");
        }

        _serverSalt = selectedSalt.Value;
        _serverSaltValidUntil = selectedValidUntil;
        _serverSaltInitialized = true;
    }

    private int GetInvalidMessageIdErrorCode(long messageId)
    {
        if (messageId <= _time.FiveMinutesAgo)
        {
            return 20;
        }

        if (messageId >= _time.ThirtySecondsLater)
        {
            return 17;
        }

        if (messageId % 4 != 0)
        {
            return 18;
        }

        if (_receivedMessageIds.Contains(messageId))
        {
            return 19;
        }

        return 16;
    }
    
    public MTProtoMessage GenerateSessionCreated(long firstMessageId, long serverSalt)
    {
        byte[] payload;
        using (var newSessionCreated = Ferrite.TL.mtproto.NewSessionCreated.Builder()
                   .FirstMsgId(firstMessageId)
                   .UniqueId(UniqueSessionId)
                   .ServerSalt(serverSalt)
                   .Build())
        {
            payload = newSessionCreated.TLBytes!.Value.AsSpan().ToArray();
        }

        MTProtoMessage newSessionMessage = new()
        {
            Data = payload,
            IsContentRelated = false,
            IsResponse = false,
            SessionId = SessionId,
            MessageType = MTProtoMessageType.NewSession
        };
        return newSessionMessage;
    }
}
