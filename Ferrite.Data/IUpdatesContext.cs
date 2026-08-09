// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Data;

public interface IUpdatesContext: IMessageBox, ISecretMessageBox
{
    /// <summary>
    /// Returns the number of sent updates.
    /// </summary>
    public Task<int> Seq();
    /// <summary>
    ///  Increments the current event sequence number.
    /// </summary>
    /// <returns>Event sequence number after increment.</returns>
    public Task<int> IncrementSeq();

    /// <summary>
    /// Marks a PTS reservation-to-publication window. State reads wait for all
    /// such windows to close so a state response cannot overtake its live update.
    /// </summary>
    public ValueTask BeginPtsPublication() => ValueTask.CompletedTask;
    public ValueTask CompletePtsPublication() => ValueTask.CompletedTask;
    public ValueTask<int> PendingPtsPublications() => ValueTask.FromResult(0);
    public ValueTask WaitForPtsPublications() => ValueTask.CompletedTask;
}
