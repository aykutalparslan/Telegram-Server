// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer.account;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class GetChatThemesHandler
{
    [TLFunction(Constructors.baseLayer_GetChatThemes)]
    public ValueTask<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var result = Themes.Builder()
            .Hash(0)
            .ThemesProperty(new Vector())
            .Build();
        return ValueTask.FromResult(result.TLBytes!.Value);
    }
}
