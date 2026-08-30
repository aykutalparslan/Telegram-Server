// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;

namespace Ferrite.Services.Updates;

public interface IUpdatesService
{
    public Task<bool> EnqueueUpdate(long userId, TLUpdate update);
    public Task<bool> EnqueueUpdate(long userId, TLUpdate update, UpdateDeliveryScope scope);

    public Task<bool> EnqueueMessageUpdate(long userId, TLUpdate update, int pts);
    public Task<int> IncrementUpdatesSequence(long authKeyId);
}
