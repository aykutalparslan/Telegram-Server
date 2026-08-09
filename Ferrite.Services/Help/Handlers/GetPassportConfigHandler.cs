// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer.help;

namespace Ferrite.Services.Handlers.HelpMethods;

public sealed class GetPassportConfigHandler
{
    [TLFunction(Constructors.baseLayer_GetPassportConfig)]
    public ValueTask<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var result = PassportConfigNotModified.Builder().Build();
        return ValueTask.FromResult(result.TLBytes!.Value);
    }
}
