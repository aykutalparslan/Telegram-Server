// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class ReorderPinnedSavedDialogsHandler
{
    [TLFunction(Constructors.baseLayer_ReorderPinnedSavedDialogs)]
    public ValueTask<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var result = BoolTrue.Builder().Build();
        return ValueTask.FromResult(result.TLBytes!.Value);
    }
}
