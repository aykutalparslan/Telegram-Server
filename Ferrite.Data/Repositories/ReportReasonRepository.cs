// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public class ReportReasonRepository : IReportReasonRepository
{
    private readonly IKVStore _store;
    public ReportReasonRepository(IKVStore store)
    {
        _store = store;
        _store.SetSchema(new TableDefinition("ferrite", "report_reasons",
            new KeyDefinition("pk",
                new DataColumn { Name = "reported_by", Type = DataType.Long },
                new DataColumn { Name = "peer_type", Type = DataType.Int },
                new DataColumn { Name = "peer_id", Type = DataType.Long })));
    }
    public bool PutPeerReportReason(long reportedByUser, int peerType, long peerId, TLReportReasonWithMessage reason)
    {
        return _store.Put(reason.AsSpan().ToArray(), reportedByUser,
            peerType, peerId);
    }
}