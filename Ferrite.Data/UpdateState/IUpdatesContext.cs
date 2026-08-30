// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.MessageBoxes;

namespace Ferrite.Data.UpdateState;

public interface IUpdatesContext: IMessageBox, ISecretMessageBox
{
    public Task<int> Seq();
    public Task<int> IncrementSeq();

    public ValueTask BeginPtsPublication() => ValueTask.CompletedTask;
    public ValueTask CompletePtsPublication() => ValueTask.CompletedTask;
    public ValueTask<int> PendingPtsPublications() => ValueTask.FromResult(0);
    public ValueTask WaitForPtsPublications() => ValueTask.CompletedTask;

    public ValueTask<int> DeliveredPts() => ValueTask.FromResult(0);
    public ValueTask AdvanceDeliveredPts(int pts) => ValueTask.CompletedTask;
}
