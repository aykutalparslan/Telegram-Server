// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL;
using Ferrite.TL.baseLayer.account;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class ReorderUsernamesHandler : ProfileSettingsHandlerBase
{
    public ReorderUsernamesHandler(ProfileStore store) : base(store) { }

    [TLFunction(Constructors.baseLayer_AccountReorderUsernames)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await GetUserIdAsync(authKeyId);
        if (!userId.HasValue) return AuthError();
        VectorOfString vector = new AccountReorderUsernames(q.AsSpan()).Order;
        var order = new List<string>(vector.Count);
        for (int i = 0; i < vector.Count; i++)
            order.Add(Encoding.UTF8.GetString(vector.ReadTLBytes()));
        return await Store.ReorderUsernamesAsync(userId.Value, order);
    }
}
