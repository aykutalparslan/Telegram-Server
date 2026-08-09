// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer.help;

namespace Ferrite.Services.Handlers.HelpMethods;

public sealed class GetPeerColorsHandler
{
    [TLFunction(Constructors.baseLayer_GetPeerColors)]
    public ValueTask<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var result = PeerColors.Builder()
            .Hash(0)
            .Colors(new Vector())
            .Build();
        return ValueTask.FromResult(result.TLBytes!.Value);
    }
}
