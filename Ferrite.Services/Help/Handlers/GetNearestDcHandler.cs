// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;

namespace Ferrite.Services.Handlers.HelpMethods;

public sealed class GetNearestDcHandler
{
    [TLFunction(Constructors.baseLayer_GetNearestDc)]
    public ValueTask<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var result = NearestDc.Builder()
            .Country("tr"u8)
            .ThisDc(1)
            .NearestDcProperty(1)
            .Build();
        return ValueTask.FromResult(result.TLBytes!.Value);
    }
}
