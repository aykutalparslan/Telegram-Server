// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class GetAvailableReactionsHandler
{
    [TLFunction(Constructors.baseLayer_GetAvailableReactions)]
    public ValueTask<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var request = (GetAvailableReactions)q;
        if (request.Hash == DefaultReactions.Hash)
        {
            var notModified = AvailableReactionsNotModified.Builder().Build();
            return ValueTask.FromResult(notModified.TLBytes!.Value);
        }

        byte[] bytes = DefaultReactions.AvailableReactionsBytes.ToArray();
        return ValueTask.FromResult(new TLBytes(bytes, 0, bytes.Length));
    }
}
