// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class GetAvailableEffectsHandler
{
    [TLFunction(Constructors.baseLayer_GetAvailableEffects)]
    public ValueTask<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var result = AvailableEffects.Builder()
            .Hash(0)
            .Effects(new Vector())
            .Documents(new Vector())
            .Build();
        return ValueTask.FromResult(result.TLBytes!.Value);
    }
}
