// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.updates;

namespace Ferrite.Services.Updates;

public interface IUpdatesStateService
{
    Task<TLState> GetState(long authKeyId);
}
