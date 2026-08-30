// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using Autofac;
using Ferrite.Core.Connection;
using Ferrite.Services.Calls;
using Ferrite.Services.Scheduling;
using Ferrite.Services.Sessions;
using Ferrite.Core.Execution;
using Ferrite.Transport;
using Ferrite.Utils;

namespace Ferrite.Core;

public class FerriteServer : IFerriteServer
{
    private enum ServerState
    {
        Created,
        Running,
        Stopped
    }

    private readonly ILifetimeScope _scope;
    private readonly IConnectionListener _socketListener;
    private readonly IMessagePipe _pipe;
    private readonly DeliveredPtsRecorder _deliveredPts;
    private Task? _pipeReceiveTask;
    private readonly ISessionService _sessionManager;
    private readonly ISecretChatMaintenance _secretChatMaintenance;
    private readonly ICallMediaRelay _callMediaRelay;
    private readonly GroupCallDisconnectMonitor _groupCallDisconnects;
    private readonly GroupCallSourcesChangedMonitor _groupCallSourcesChanged;
    private readonly GroupCallMediaRuntime _groupCallMediaRuntime;
    private readonly GroupCallBroadcastRuntime _groupCallBroadcastRuntime;
    private readonly GroupCallRecordingRuntime _groupCallRecordingRuntime;
    private readonly ScheduledMessageRuntime _scheduledMessages;
    private readonly MessageExpiryRuntime _messageExpiry;
    private readonly ICallRegistry _callRegistry;
    private readonly CallTerminator _callTerminator;
    private readonly IMTProtoTime _time;
    private readonly ILogger _log;
    private readonly ConcurrentDictionary<MTProtoConnection, byte> _connections =
        new();
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private volatile ServerState _state = ServerState.Created;
    private CancellationTokenSource? _stopping;
    private Task? _acceptLoop;
    public FerriteServer(ILifetimeScope scope)
    {
        _scope = scope;
        _socketListener = _scope.Resolve<IConnectionListener>();
        _sessionManager = _scope.Resolve<ISessionService>();
        _secretChatMaintenance = _scope.Resolve<ISecretChatMaintenance>();
        _callMediaRelay = _scope.Resolve<ICallMediaRelay>();
        _groupCallDisconnects = _scope.Resolve<GroupCallDisconnectMonitor>();
        _groupCallSourcesChanged = _scope.Resolve<GroupCallSourcesChangedMonitor>();
        _groupCallMediaRuntime = _scope.Resolve<GroupCallMediaRuntime>();
        _groupCallBroadcastRuntime = _scope.Resolve<GroupCallBroadcastRuntime>();
        _groupCallRecordingRuntime = _scope.Resolve<GroupCallRecordingRuntime>();
        _scheduledMessages = _scope.Resolve<ScheduledMessageRuntime>();
        _messageExpiry = _scope.Resolve<MessageExpiryRuntime>();
        _callRegistry = _scope.Resolve<ICallRegistry>();
        _callTerminator = _scope.Resolve<CallTerminator>();
        _time = _scope.Resolve<IMTProtoTime>();
        _pipe = _scope.Resolve<IMessagePipe>();
        _deliveredPts = _scope.Resolve<DeliveredPtsRecorder>();
        _log = _scope.Resolve<ILogger>();
    }

    public IPEndPoint? BoundEndpoint => _state == ServerState.Running
        ? _socketListener.EndPoint as IPEndPoint
        : null;

    public IPEndPoint? CallMediaBoundEndpoint => _state == ServerState.Running
        ? _callMediaRelay.BoundEndpoint
        : null;

    public CallMediaRelaySnapshot CallMediaRelaySnapshot => new(
        _callMediaRelay.IsReady, _callMediaRelay.AllocationCount,
        _callMediaRelay.ForwardedPackets, _callMediaRelay.ForwardedBytes,
        _callMediaRelay.DroppedPackets);

    public FerriteReadinessSnapshot Readiness => new(
        _state == ServerState.Running,
        ToPipelineSnapshot(_groupCallMediaRuntime.Snapshot),
        ToPipelineSnapshot(_groupCallBroadcastRuntime.Snapshot),
        ToPipelineSnapshot(_groupCallRecordingRuntime.Snapshot));

    public async Task StartAsync(IPEndPoint endPoint, CancellationToken token)
    {
        Task acceptLoop = await StartCoreAsync(endPoint, token);
        await acceptLoop;
    }

    private async Task<Task> StartCoreAsync(IPEndPoint endPoint,
        CancellationToken token)
    {
        await _lifecycleGate.WaitAsync(token);
        try
        {
            if (_state != ServerState.Created)
            {
                throw new InvalidOperationException(
                    $"The server cannot start from the {_state} state.");
            }

            var rollback = new List<Func<Task>>();
            try
            {
                token.ThrowIfCancellationRequested();
                await _pipe.SubscribeAsync(
                    MessagePipeChannels.ForNode(_sessionManager.NodeId));
                rollback.Add(async () => await _pipe.UnSubscribeAsync());

                token.ThrowIfCancellationRequested();
                var stopping = new CancellationTokenSource();
                _stopping = stopping;
                Task pipeReceiveTask = DoReceive(stopping.Token);
                _pipeReceiveTask = pipeReceiveTask;
                rollback.Add(async () =>
                {
                    stopping.Cancel();
                    await pipeReceiveTask;
                    stopping.Dispose();
                    _stopping = null;
                    _pipeReceiveTask = null;
                });

                token.ThrowIfCancellationRequested();
                await _secretChatMaintenance.StartAsync(token);
                rollback.Add(async () => await _secretChatMaintenance
                    .StopAsync(CancellationToken.None));

                token.ThrowIfCancellationRequested();
                await _callMediaRelay.StartAsync(token);
                rollback.Add(() => _callMediaRelay.StopAsync());

                token.ThrowIfCancellationRequested();
                await _groupCallDisconnects.StartAsync(token);
                rollback.Add(() => _groupCallDisconnects
                    .StopAsync(CancellationToken.None));

                token.ThrowIfCancellationRequested();
                await _groupCallSourcesChanged.StartAsync(token);
                rollback.Add(() => _groupCallSourcesChanged
                    .StopAsync(CancellationToken.None));

                token.ThrowIfCancellationRequested();
                await _groupCallMediaRuntime.StartAsync(token);
                rollback.Add(() => _groupCallMediaRuntime
                    .StopAsync(CancellationToken.None));

                token.ThrowIfCancellationRequested();
                await _groupCallBroadcastRuntime.StartAsync(token);
                rollback.Add(() => _groupCallBroadcastRuntime
                    .StopAsync(CancellationToken.None));

                token.ThrowIfCancellationRequested();
                await _groupCallRecordingRuntime.StartAsync(token);
                rollback.Add(() => _groupCallRecordingRuntime
                    .StopAsync(CancellationToken.None));

                _callRegistry.SetDeadlineExpiredHandler(OnCallDeadlineExpired);
                rollback.Add(() =>
                {
                    _callRegistry.SetDeadlineExpiredHandler(null);
                    return Task.CompletedTask;
                });

                token.ThrowIfCancellationRequested();
                await _scheduledMessages.StartAsync(token);
                rollback.Add(async () => await _scheduledMessages
                    .StopAsync(CancellationToken.None));

                token.ThrowIfCancellationRequested();
                await _messageExpiry.StartAsync(token);
                rollback.Add(async () => await _messageExpiry
                    .StopAsync(CancellationToken.None));

                token.ThrowIfCancellationRequested();
                _socketListener.Bind(endPoint);
            }
            catch
            {
                await RollbackAsync(rollback);
                throw;
            }

            _state = ServerState.Running;
            _acceptLoop = StartAccept(_socketListener, token);
            return _acceptLoop;
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    private async Task RollbackAsync(List<Func<Task>> rollback)
    {
        for (int i = rollback.Count - 1; i >= 0; i--)
        {
            try
            {
                await rollback[i]();
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Server startup rollback step failed.");
            }
        }
    }

    public async Task StopAsync(CancellationToken token)
    {
        await _lifecycleGate.WaitAsync(token);
        try
        {
            if (_state == ServerState.Stopped)
            {
                return;
            }

            if (_state == ServerState.Running)
            {
                _callRegistry.SetDeadlineExpiredHandler(null);
                await _socketListener.UnbindAsync(token);
                if (_acceptLoop is not null)
                {
                    await _acceptLoop.WaitAsync(token);
                    _acceptLoop = null;
                }

                MTProtoConnection[] connections = _connections.Keys.ToArray();
                await Task.WhenAll(connections.Select(connection => connection
                    .StopAsync(new OperationCanceledException(
                        "Ferrite server is stopping.")).AsTask()));
                _connections.Clear();

                await _messageExpiry.StopAsync(token);
                await _scheduledMessages.StopAsync(token);
                await _secretChatMaintenance.StopAsync(token);
                await _callMediaRelay.StopAsync();
                await _groupCallRecordingRuntime.StopAsync(token);
                await _groupCallBroadcastRuntime.StopAsync(token);
                await _groupCallMediaRuntime.StopAsync(token);
                await _groupCallSourcesChanged.StopAsync(token);
                await _groupCallDisconnects.StopAsync(token);

                _stopping?.Cancel();
                if (_pipeReceiveTask is not null)
                {
                    try
                    {
                        await _pipeReceiveTask.WaitAsync(token);
                    }
                    catch (OperationCanceledException) when (!token.IsCancellationRequested)
                    {
                    }
                    _pipeReceiveTask = null;
                }
                await _pipe.UnSubscribeAsync();
                _stopping?.Dispose();
                _stopping = null;
            }

            _state = ServerState.Stopped;
        }
        finally
        {
            _lifecycleGate.Release();
        }

        await _scope.DisposeAsync();
    }

    private static FerritePipelineReadinessSnapshot ToPipelineSnapshot(
        GroupCallMediaRuntimeSnapshot snapshot) => new(
        snapshot.Status switch
        {
            GroupCallMediaRuntimeStatus.Starting =>
                FerritePipelineReadinessStatus.Starting,
            GroupCallMediaRuntimeStatus.Ready =>
                FerritePipelineReadinessStatus.Ready,
            GroupCallMediaRuntimeStatus.Degraded =>
                FerritePipelineReadinessStatus.Degraded,
            _ => FerritePipelineReadinessStatus.Stopped,
        }, snapshot.Failure);

    private static FerritePipelineReadinessSnapshot ToPipelineSnapshot(
        GroupCallBroadcastRuntimeSnapshot snapshot) => new(
        snapshot.Status switch
        {
            GroupCallBroadcastRuntimeStatus.Starting =>
                FerritePipelineReadinessStatus.Starting,
            GroupCallBroadcastRuntimeStatus.Ready =>
                FerritePipelineReadinessStatus.Ready,
            GroupCallBroadcastRuntimeStatus.Degraded =>
                FerritePipelineReadinessStatus.Degraded,
            _ => FerritePipelineReadinessStatus.Stopped,
        }, snapshot.Failure);

    private static FerritePipelineReadinessSnapshot ToPipelineSnapshot(
        GroupCallRecordingRuntimeSnapshot snapshot) => new(
        snapshot.Status switch
        {
            GroupCallRecordingRuntimeStatus.NotConfigured =>
                FerritePipelineReadinessStatus.NotConfigured,
            GroupCallRecordingRuntimeStatus.Starting =>
                FerritePipelineReadinessStatus.Starting,
            GroupCallRecordingRuntimeStatus.Ready =>
                FerritePipelineReadinessStatus.Ready,
            GroupCallRecordingRuntimeStatus.Degraded =>
                FerritePipelineReadinessStatus.Degraded,
            _ => FerritePipelineReadinessStatus.Stopped,
        }, snapshot.Failure);

    private async Task StartAccept(IConnectionListener socketListener,
        CancellationToken cancellationToken)
    {
        _log.Information(String.Format("Server is listening at {0}", socketListener.EndPoint));
        try
        {
            while (true)
            {
                var connection = await socketListener.AcceptAsync(cancellationToken);
                if (connection == null)
                {
                    break;
                }

                _log.Debug("New MTProto connection was created.");
                MTProtoConnection? mtProtoConnection = null;
                try
                {
                    connection.Start();
                    mtProtoConnection = _scope.Resolve<MTProtoConnection>(
                        new NamedParameter("connection", connection));
                    mtProtoConnection.Stopped += OnConnectionStopped;
                    _connections.TryAdd(mtProtoConnection, 0);
                    mtProtoConnection.Start();
                }
                catch (Exception e)
                {
                    if (mtProtoConnection is not null)
                    {
                        await mtProtoConnection.StopAsync(e);
                    }
                    else
                    {
                        connection.Abort(e);
                        await connection.DisposeAsync();
                    }
                    _log.Fatal(e, e.Message);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void OnConnectionStopped(MTProtoConnection connection)
    {
        connection.Stopped -= OnConnectionStopped;
        _connections.TryRemove(connection, out _);
    }

    private void OnCallDeadlineExpired(long callId, CallDeadlineKind kind)
    {
        int now = checked((int)_time.GetUnixTimeInSeconds());
        CallRegistryResult result = _callRegistry.TryExpire(callId, kind,
            Ferrite.TL.Constructors.baseLayer_PhoneCallDiscardReasonMissed, now);
        if (result.IsOk && result.Call is not null)
        {
            _ = FinalizeExpiredCallAsync(result.Call);
        }
    }

    private async Task FinalizeExpiredCallAsync(CallSnapshot call)
    {
        try
        {
            await _callTerminator.FinalizeAsync(call, invokerUserId: null,
                invokerAuthKeyId: null);
        }
        catch (Exception ex)
        {
            _log.Error(ex, "Call timeout finalization failed.");
        }
    }

    private async Task DoReceive(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await _pipe.ReadMessageAsync(cancellationToken);
                try
                {
                    var message = MTProtoMessageEnvelope.Deserialize(result);
                    if (message is { MessageType: MTProtoMessageType.Unencrypted } &&
                        message.Nonce != null)
                    {
                        var sessionExists = _sessionManager.TryGetLocalAuthSession(message.Nonce, out var protoSession);
                        if (sessionExists &&
                            protoSession.TryGetConnection(out var connection) &&
                            !connection.IsEncrypted)
                        {
                            _ = connection.SendAsync(message);
                        }
                    }
                    else
                    {
                        var sessionExists = _sessionManager.TryGetLocalSession(message.SessionId, out var protoSession);
                        if (sessionExists &&
                            protoSession.TryGetConnection(out var connection))
                        {
                            _log.Debug($"==> delivered to session {message.SessionId} ==<");
                            await connection.SendAsync(message);
                            await _deliveredPts.RecordAsync(message);
                        }
                        else
                        {
                            _log.Debug($"==> session {message.SessionId} is not on this node " +
                                       $"(known:{sessionExists}) ==<");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _log.Error(ex, ex.Message);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}
