// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Ferrite.Crypto;
using Ferrite.Services.Calls;
using Ferrite.Utils;

namespace Ferrite.Core.Calls;

public sealed class TelegramCallReflector : ICallMediaRelay, IDisposable
{
    private const int PeerTagLength = 16;
    private const int PrefixLength = 12;
    private const int DataHeaderLength = PeerTagLength + 4 + 4;
    private const int SelfInfoRequestLength = 40;
    private const int SelfInfoResponseLength = 64;
    private const uint SelfInfoMagic = 0xc01572c7;

    private readonly CallMediaRelayOptions _options;
    private readonly IRandomGenerator _random;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _log;
    private readonly object _lifecycleGate = new();
    private readonly ConcurrentDictionary<RelayTagKey, RelayAllocation> _allocations = new();
    private readonly ConcurrentDictionary<long, RelayTagKey> _allocationsByCallId = new();
    private Socket? _socket;
    private Task? _receiveLoop;
    private CancellationTokenSource? _cts;
    private ITimer? _sweepTimer;
    private volatile bool _ready;
    private long _forwardedPackets;
    private long _forwardedBytes;
    private long _droppedPackets;

    public TelegramCallReflector(CallMediaRelayOptions options,
        IRandomGenerator random, TimeProvider timeProvider, ILogger log)
    {
        _options = options;
        _random = random;
        _timeProvider = timeProvider;
        _log = log;
    }

    public bool IsReady => _ready;

    public IPEndPoint? BoundEndpoint { get; private set; }

    public IPEndPoint? AdvertisedEndpoint { get; private set; }

    public int AllocationCount => _allocations.Count;

    public long ForwardedPackets => Interlocked.Read(ref _forwardedPackets);

    public long ForwardedBytes => Interlocked.Read(ref _forwardedBytes);

    public long DroppedPackets => Interlocked.Read(ref _droppedPackets);

    public Task StartAsync(CancellationToken cancellationToken)
    {
        lock (_lifecycleGate)
        {
            if (_ready)
            {
                throw new InvalidOperationException(
                    "The call reflector is already running.");
            }

            Socket socket = new(AddressFamily.InterNetwork, SocketType.Dgram,
                ProtocolType.Udp);
            try
            {
                socket.Bind(new IPEndPoint(IPAddress.Parse(_options.BindAddress),
                    _options.BindPort));
            }
            catch
            {
                socket.Dispose();
                throw;
            }

            _socket = socket;
            BoundEndpoint = (IPEndPoint)socket.LocalEndPoint!;
            int advertisedPort = _options.AdvertisedPort != 0
                ? _options.AdvertisedPort
                : BoundEndpoint.Port;
            AdvertisedEndpoint = new IPEndPoint(
                IPAddress.Parse(_options.AdvertisedAddress), advertisedPort);
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _receiveLoop = Task.Run(() => ReceiveLoop(socket, _cts.Token));
            _sweepTimer = _timeProvider.CreateTimer(_ => SweepIdleAllocations(),
                null, _options.IdleSweepInterval, _options.IdleSweepInterval);
            _ready = true;
            _log.Information($"📞 Call reflector listening on {BoundEndpoint}, " +
                             $"advertising {AdvertisedEndpoint}");
            return Task.CompletedTask;
        }
    }

    public async Task StopAsync()
    {
        Task? receiveLoop;
        lock (_lifecycleGate)
        {
            if (!_ready && _socket == null)
            {
                return;
            }

            _ready = false;
            _sweepTimer?.Dispose();
            _sweepTimer = null;
            _cts?.Cancel();
            _socket?.Dispose();
            _socket = null;
            receiveLoop = _receiveLoop;
            _receiveLoop = null;
        }

        if (receiveLoop != null)
        {
            try
            {
                await receiveLoop;
            }
            catch (Exception ex)
            {
                _log.Debug(ex, "Call reflector receive loop ended with an exception");
            }
        }

        lock (_lifecycleGate)
        {
            _cts?.Dispose();
            _cts = null;
            _allocations.Clear();
            _allocationsByCallId.Clear();
            BoundEndpoint = null;
            AdvertisedEndpoint = null;
        }
    }

    public CallRelayAllocation? CreateAllocation(long callId)
    {
        if (!_ready)
        {
            return null;
        }

        while (true)
        {
            byte[] peerTag = _random.GetRandomBytes(PeerTagLength);
            var key = RelayTagKey.FromPrefix(peerTag);
            var allocation = new RelayAllocation(callId, peerTag,
                _timeProvider.GetUtcNow().UtcTicks);
            if (!_allocations.TryAdd(key, allocation))
            {
                continue;
            }

            if (!_allocationsByCallId.TryAdd(callId, key))
            {
                _allocations.TryRemove(key, out _);
                return _allocationsByCallId.TryGetValue(callId, out RelayTagKey existing) &&
                       _allocations.TryGetValue(existing, out RelayAllocation? current)
                    ? new CallRelayAllocation(callId, current.PeerTag.ToArray())
                    : null;
            }

            return new CallRelayAllocation(callId, peerTag.ToArray());
        }
    }

    public bool RemoveAllocation(long callId)
    {
        if (!_allocationsByCallId.TryRemove(callId, out RelayTagKey key))
        {
            return false;
        }

        if (_allocations.TryRemove(key, out RelayAllocation? allocation))
        {
            allocation.MarkRemoved();
        }

        return true;
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
    }

    private async Task ReceiveLoop(Socket socket, CancellationToken ct)
    {
        var pool = ArrayPool<byte>.Shared;
        EndPoint anyEndpoint = new IPEndPoint(IPAddress.Any, 0);
        while (!ct.IsCancellationRequested)
        {
            byte[] buffer = pool.Rent(_options.MaxDatagramSize + 1);
            try
            {
                SocketReceiveFromResult result;
                try
                {
                    result = await socket.ReceiveFromAsync(buffer, SocketFlags.None,
                        anyEndpoint, ct);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (SocketException ex)
                {
                    if (ct.IsCancellationRequested)
                    {
                        return;
                    }

                    _log.Debug(ex, "Call reflector receive failed; continuing");
                    continue;
                }

                try
                {
                    await ProcessDatagram(socket, buffer,
                        result.ReceivedBytes, (IPEndPoint)result.RemoteEndPoint, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    Interlocked.Increment(ref _droppedPackets);
                    _log.Debug(ex, "Call reflector failed to process a datagram");
                }
            }
            finally
            {
                pool.Return(buffer);
            }
        }
    }

    private async ValueTask ProcessDatagram(Socket socket, byte[] buffer, int length,
        IPEndPoint source, CancellationToken ct)
    {
        if (length < PeerTagLength || length > _options.MaxDatagramSize)
        {
            Interlocked.Increment(ref _droppedPackets);
            return;
        }

        RelayTagKey key = RelayTagKey.FromPrefix(buffer);
        if (!_allocations.TryGetValue(key, out RelayAllocation? allocation) ||
            allocation.Removed)
        {
            Interlocked.Increment(ref _droppedPackets);
            return;
        }

        long nowTicks = _timeProvider.GetUtcNow().UtcTicks;
        allocation.Touch(nowTicks);

        if (IsSpecialPacket(buffer, length))
        {
            uint senderTag = MemoryMarshal.Read<uint>(buffer.AsSpan(PrefixLength, 4));
            if (senderTag == 0)
            {
                Interlocked.Increment(ref _droppedPackets);
                return;
            }

            allocation.LearnRoute(senderTag, source, nowTicks,
                _options.MaxParticipantTagsPerAllocation);
            if (length >= SelfInfoRequestLength &&
                buffer[28] == 0xFE && buffer[29] == 0xFF &&
                buffer[30] == 0xFF && buffer[31] == 0xFF)
            {
                await SendSelfInfo(socket, buffer, source, ct);
            }

            return;
        }

        if (length < DataHeaderLength)
        {
            Interlocked.Increment(ref _droppedPackets);
            return;
        }

        uint destinationTag = MemoryMarshal.Read<uint>(buffer.AsSpan(PrefixLength, 4));
        uint dataSenderTag = MemoryMarshal.Read<uint>(buffer.AsSpan(PeerTagLength, 4));
        uint declaredLength = BinaryPrimitives.ReadUInt32BigEndian(
            buffer.AsSpan(PeerTagLength + 4, 4));
        int available = length - DataHeaderLength;
        if (dataSenderTag == 0 || destinationTag == 0 ||
            declaredLength > (uint)available || available - (int)declaredLength > 3)
        {
            Interlocked.Increment(ref _droppedPackets);
            return;
        }

        for (int i = DataHeaderLength + (int)declaredLength; i < length; i++)
        {
            if (buffer[i] != 0)
            {
                Interlocked.Increment(ref _droppedPackets);
                return;
            }
        }

        allocation.LearnRoute(dataSenderTag, source, nowTicks,
            _options.MaxParticipantTagsPerAllocation);
        if (!allocation.TryGetRoute(destinationTag, out IPEndPoint? destination))
        {
            Interlocked.Increment(ref _droppedPackets);
            return;
        }

        await socket.SendToAsync(buffer.AsMemory(0, length), SocketFlags.None,
            destination!, ct);
        Interlocked.Increment(ref _forwardedPackets);
        Interlocked.Add(ref _forwardedBytes, length);
    }

    private static bool IsSpecialPacket(byte[] buffer, int length)
    {
        if (length < PeerTagLength + 12)
        {
            return false;
        }

        for (int i = PeerTagLength; i < PeerTagLength + 12; i++)
        {
            if (buffer[i] != 0xFF)
            {
                return false;
            }
        }

        return true;
    }

    private async ValueTask SendSelfInfo(Socket socket, byte[] request,
        IPEndPoint source, CancellationToken ct)
    {
        if (source.AddressFamily != AddressFamily.InterNetwork)
        {
            Interlocked.Increment(ref _droppedPackets);
            return;
        }

        byte[] response = new byte[SelfInfoResponseLength];
        Span<byte> span = response;
        request.AsSpan(0, PeerTagLength).CopyTo(span);
        span.Slice(16, 12).Fill(0xFF);
        BinaryPrimitives.WriteUInt32LittleEndian(span[28..],
            SelfInfoMagic);
        int date = checked((int)_timeProvider.GetUtcNow().ToUnixTimeSeconds());
        BinaryPrimitives.WriteInt32LittleEndian(span[32..], date);
        request.AsSpan(32, 8).CopyTo(span[36..]);
        span[54] = 0xFF;
        span[55] = 0xFF;
        source.Address.TryWriteBytes(span.Slice(56, 4), out _);
        BinaryPrimitives.WriteUInt32LittleEndian(span[60..], (uint)source.Port);
        await socket.SendToAsync(response, SocketFlags.None, source, ct);
    }

    private void SweepIdleAllocations()
    {
        long cutoff = _timeProvider.GetUtcNow().UtcTicks -
                      _options.AllocationIdleTimeout.Ticks;
        foreach (var pair in _allocations)
        {
            RelayAllocation allocation = pair.Value;
            if (allocation.LastActivityTicks > cutoff)
            {
                continue;
            }

            allocation.MarkRemoved();
            _allocations.TryRemove(pair.Key, out _);
            _allocationsByCallId.TryRemove(
                new KeyValuePair<long, RelayTagKey>(allocation.CallId, pair.Key));
            _log.Debug($"📞 Call reflector expired idle allocation for call " +
                       $"{allocation.CallId}");
        }
    }

    private readonly record struct RelayTagKey(ulong High, uint Low)
    {
        public static RelayTagKey FromPrefix(ReadOnlySpan<byte> bytes) => new(
            MemoryMarshal.Read<ulong>(bytes[..8]),
            MemoryMarshal.Read<uint>(bytes.Slice(8, 4)));
    }

    private sealed class RelayAllocation
    {
        private readonly object _routeGate = new();
        private readonly Dictionary<uint, ParticipantRoute> _routes = new();
        private long _lastActivityTicks;
        private volatile bool _removed;

        public RelayAllocation(long callId, byte[] peerTag, long nowTicks)
        {
            CallId = callId;
            PeerTag = peerTag;
            _lastActivityTicks = nowTicks;
        }

        public long CallId { get; }

        public byte[] PeerTag { get; }

        public bool Removed => _removed;

        public long LastActivityTicks => Interlocked.Read(ref _lastActivityTicks);

        public void Touch(long nowTicks) =>
            Interlocked.Exchange(ref _lastActivityTicks, nowTicks);

        public void MarkRemoved() => _removed = true;

        public void LearnRoute(uint participantTag, IPEndPoint endpoint,
            long nowTicks, int maxRoutes)
        {
            lock (_routeGate)
            {
                if (_routes.TryGetValue(participantTag, out ParticipantRoute? route))
                {
                    route.Endpoint = endpoint;
                    route.LastActivityTicks = nowTicks;
                    return;
                }

                if (_routes.Count >= maxRoutes)
                {
                    uint oldestTag = 0;
                    long oldestActivity = long.MaxValue;
                    foreach (var pair in _routes)
                    {
                        if (pair.Value.LastActivityTicks < oldestActivity)
                        {
                            oldestActivity = pair.Value.LastActivityTicks;
                            oldestTag = pair.Key;
                        }
                    }

                    _routes.Remove(oldestTag);
                }

                _routes[participantTag] = new ParticipantRoute
                {
                    Endpoint = endpoint,
                    LastActivityTicks = nowTicks,
                };
            }
        }

        public bool TryGetRoute(uint participantTag, out IPEndPoint? endpoint)
        {
            lock (_routeGate)
            {
                if (_routes.TryGetValue(participantTag, out ParticipantRoute? route))
                {
                    endpoint = route.Endpoint;
                    return true;
                }
            }

            endpoint = null;
            return false;
        }

        private sealed class ParticipantRoute
        {
            public required IPEndPoint Endpoint;
            public long LastActivityTicks;
        }
    }
}
