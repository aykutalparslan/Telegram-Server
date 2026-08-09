// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;

namespace Ferrite.Services.Handlers.HelpMethods;

public sealed class GetCdnConfigHandler
{
    [TLFunction(Constructors.baseLayer_GetCdnConfig)]
    public ValueTask<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var result = CdnConfig.Builder()
            .PublicKeys(new Vector())
            .Build();
        return ValueTask.FromResult(result.TLBytes!.Value);
    }
}
