// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Runtime.CompilerServices;
using Ferrite.TL.baseLayer.dto;
using Ferrite.Data.Models;

namespace Ferrite.Data.Repositories;

public sealed class ModerationRepository : IModerationRepository
{
    private readonly IKVStore _actionBars;
    private readonly IKVStore _reports;

    public ModerationRepository(IKVStore actionBars, IKVStore reports)
    {
        _actionBars = actionBars;
        _reports = reports;
        actionBars.SetSchema(new TableDefinition("ferrite", "peer_action_bars",
            new KeyDefinition("pk",
                new DataColumn { Name = "user_id", Type = DataType.Long },
                new DataColumn { Name = "peer_type", Type = DataType.Int },
                new DataColumn { Name = "peer_id", Type = DataType.Long })));
        reports.SetSchema(new TableDefinition("ferrite", "moderation_reports",
            new KeyDefinition("pk",
                new DataColumn { Name = "reporter_user_id", Type = DataType.Long },
                new DataColumn { Name = "report_id", Type = DataType.Long })));
    }

    public bool PutActionBarState(TLPeerActionBarState state)
    {
        var info = state.AsPeerActionBarState();
        return _actionBars.Put(state.AsSpan().ToArray(), info.UserId,
            info.PeerType, info.PeerId);
    }

    public async ValueTask<TLPeerActionBarState?> GetActionBarStateAsync(long userId,
        int peerType, long peerId)
    {
        byte[]? bytes = await _actionBars.GetAsync(userId, peerType, peerId);
        return bytes is { Length: > 0 }
            ? new TLPeerActionBarState(bytes, 0, bytes.Length)
            : null;
    }

    public bool DeleteActionBarState(long userId, int peerType, long peerId) =>
        _actionBars.Delete(userId, peerType, peerId);

    public bool PutReport(TLModerationReport report)
    {
        var info = report.AsModerationReport();
        return _reports.Put(report.AsSpan().ToArray(), info.ReporterUserId,
            info.ReportId);
    }

    public async ValueTask<TLModerationReport?> GetReportAsync(long reporterUserId,
        long reportId)
    {
        byte[]? bytes = await _reports.GetAsync(reporterUserId, reportId);
        return bytes is { Length: > 0 }
            ? new TLModerationReport(bytes, 0, bytes.Length)
            : null;
    }

    public async IAsyncEnumerable<TLModerationReport> IterateReportsAsync(
        long reporterUserId,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (byte[] bytes in _reports.IterateAsync(reporterUserId)
                           .WithCancellation(cancellationToken))
        {
            if (bytes is { Length: > 0 })
            {
                yield return new TLModerationReport(bytes, 0, bytes.Length);
            }
        }
    }
}
