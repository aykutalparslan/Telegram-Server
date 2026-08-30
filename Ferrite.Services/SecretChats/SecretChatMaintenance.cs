// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.Utils;
using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.SecretChats;

public sealed class SecretChatMaintenance : ISecretChatMaintenance
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly ISecretChatsRepository _secretChatsRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IUpdatesContextFactory _updatesContextFactory;
    private readonly IMTProtoTime _time;
    private readonly SecretChatLimits _limits;
    private readonly SecretChatTelemetry _telemetry;
    private readonly ILogger _log;
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly SemaphoreSlim _runGate = new(1, 1);
    private CancellationTokenSource? _stopping;
    private Task? _loop;
    private long _afterAuthKeyId = long.MinValue;

    public SecretChatMaintenance(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, ISecretChatsRepository secretChatsRepository,
        IUpdatesContextFactory updatesContextFactory, IMTProtoTime time,
        SecretChatLimits limits, SecretChatTelemetry telemetry, ILogger log)
    {
        _authorizationRepository = authorizationRepository;

        _secretChatsRepository = secretChatsRepository;

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            limits.DedupRetentionSeconds);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            limits.MaintenanceIntervalSeconds);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            limits.MaxMaintenanceAuthKeysPerPass);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(
            limits.MaxMaintenanceItemsPerPass);
        _unitOfWork = unitOfWork;
        _updatesContextFactory = updatesContextFactory;
        _time = time;
        _limits = limits;
        _telemetry = telemetry;
        _log = log;
    }

    public async ValueTask StartAsync(
        CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            if (_loop is not null)
            {
                return;
            }
            await RunOnceAsync(cancellationToken);
            _stopping = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
            _loop = RunLoopAsync(_stopping.Token);
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public async ValueTask StopAsync(
        CancellationToken cancellationToken = default)
    {
        Task? loop;
        CancellationTokenSource? stopping = null;
        await _lifecycleGate.WaitAsync(cancellationToken);
        try
        {
            if (_loop is null)
            {
                return;
            }
            _stopping!.Cancel();
            loop = _loop;
            stopping = _stopping;
            _loop = null;
            _stopping = null;
        }
        finally
        {
            _lifecycleGate.Release();
        }

        try
        {
            await loop.WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            stopping?.Dispose();
        }
    }

    public async ValueTask<SecretChatMaintenanceRunResult> RunOnceAsync(
        CancellationToken cancellationToken = default)
    {
        await _runGate.WaitAsync(cancellationToken);
        try
        {
            int now = checked((int)_time.GetUnixTimeInSeconds());
            IReadOnlyList<long> authKeyIds = await _secretChatsRepository.GetQtsMaintenanceAuthKeyIdsAsync(
                    _afterAuthKeyId, _limits.MaxMaintenanceAuthKeysPerPass,
                    cancellationToken);
            if (authKeyIds.Count == 0 && _afterAuthKeyId != long.MinValue)
            {
                _afterAuthKeyId = long.MinValue;
                authKeyIds = await _secretChatsRepository
                    .GetQtsMaintenanceAuthKeyIdsAsync(_afterAuthKeyId,
                        _limits.MaxMaintenanceAuthKeysPerPass, cancellationToken);
            }

            int maintainedAuthKeys = 0;
            int recoveredPending = 0;
            int expiredEvents = 0;
            long expiredBytes = 0;
            int queuedEvents = 0;
            long queuedBytes = 0;
            foreach (long authKeyId in authKeyIds)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _afterAuthKeyId = authKeyId;
                TLDto.TLAuthInfo? authValue = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
                if (authValue is null)
                {
                    _telemetry.Rejection("maintenance", authKeyId, 0,
                        "authorization_missing");
                    continue;
                }

                long userId;
                bool loggedIn;
                using (TLDto.TLAuthInfo auth = authValue.Value)
                {
                    userId = auth.AsAuthInfo().UserId;
                    loggedIn = auth.AsAuthInfo().LoggedIn;
                }
                if (!loggedIn)
                {
                    _telemetry.Rejection("maintenance", authKeyId, 0,
                        "authorization_logged_out");
                    continue;
                }

                IUpdatesContext context = _updatesContextFactory
                    .GetUpdatesContext(authKeyId, userId);
                SecretChatQtsMaintenanceResult result = await _secretChatsRepository.MaintainQtsAsync(authKeyId, now,
                        context.Qts, context.IncrementQts, cancellationToken);
                maintainedAuthKeys++;
                recoveredPending += result.RecoveredPending ? 1 : 0;
                expiredEvents += result.ExpiredEvents;
                expiredBytes += result.ExpiredBytes;
                queuedEvents += result.QueuedEvents;
                queuedBytes += result.QueuedBytes;
            }

            SecretChatRetentionCleanupResult retention = await _secretChatsRepository.CleanupRetentionAsync(now,
                    _limits.DedupRetentionSeconds,
                    _limits.MaxMaintenanceItemsPerPass, cancellationToken);
            var run = new SecretChatMaintenanceRunResult(maintainedAuthKeys,
                recoveredPending, expiredEvents, expiredBytes,
                retention.DeletedReceipts, retention.DeletedControls,
                queuedEvents, queuedBytes);
            _telemetry.Cleanup(run.AuthKeys, run.RecoveredPending,
                run.ExpiredEvents, run.ExpiredBytes, run.DeletedReceipts,
                run.DeletedControls, run.QueuedEvents, run.QueuedBytes);
            return run;
        }
        finally
        {
            _runGate.Release();
        }
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(
            _limits.MaintenanceIntervalSeconds));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                try
                {
                    await RunOnceAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (
                    cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _log.Warning(exception,
                        "Secret-chat maintenance pass failed; the next bounded pass will retry.");
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}
