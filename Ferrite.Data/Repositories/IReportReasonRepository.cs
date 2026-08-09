// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public interface IReportReasonRepository
{
    public bool PutPeerReportReason(long reportedByUser, int peerType, long peerId, TLReportReasonWithMessage reason);
}