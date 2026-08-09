// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer.help;

namespace Ferrite.Services.Handlers.HelpMethods;

public sealed class GetInviteTextHandler
{
    [TLFunction(Constructors.baseLayer_GetInviteText)]
    public ValueTask<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var result = InviteText.Builder()
            .Message("Join me on Ferrite."u8)
            .Build();
        return ValueTask.FromResult(result.TLBytes!.Value);
    }
}
