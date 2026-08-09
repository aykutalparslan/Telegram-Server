// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer.help;

namespace Ferrite.Services.Handlers.HelpMethods;

public sealed class GetTermsOfServiceUpdateHandler
{
    [TLFunction(Constructors.baseLayer_GetTermsOfServiceUpdate)]
    public ValueTask<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var result = TermsOfServiceUpdateEmpty.Builder()
            .Expires((int)DateTimeOffset.Now.AddDays(1).ToUnixTimeSeconds())
            .Build();
        return ValueTask.FromResult(result.TLBytes!.Value);
    }
}
