// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Data;

public interface ISecretMessageBox
{
    /// <summary>
    /// Returns the current event sequence number.
    /// </summary>
    public ValueTask<int> Qts();
    /// <summary>
    ///  Increments the current event sequence number.
    /// </summary>
    /// <returns>Event sequence number after increment.</returns>
    public ValueTask<int> IncrementQts();
}