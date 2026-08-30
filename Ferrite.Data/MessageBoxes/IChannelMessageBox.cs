// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Data.MessageBoxes;

public interface IChannelMessageBox
{
    public ValueTask<int> Pts();
    public ValueTask<int> IncrementPts();
    public ValueTask<int> IncrementPts(int count);
    public ValueTask<int> NextMessageId();
    public ValueTask BeginPtsPublication(int ptsCount = 1);
    public ValueTask CompletePtsPublication(int ptsCount = 1);
    public ValueTask<int> PendingPtsPublications();
    public ValueTask<bool> WaitForPtsPublications(
        TimeSpan? timeout = null);
}
