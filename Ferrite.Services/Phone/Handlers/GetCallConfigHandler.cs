// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL;
using Ferrite.TL.baseLayer;

namespace Ferrite.Services.Phone.Handlers;

public sealed class GetCallConfigHandler
{
    private static readonly byte[] EmptyConfig = Encoding.UTF8.GetBytes("{}");

    [TLFunction(Constructors.baseLayer_GetCallConfig)]
    public ValueTask<TLDataJSON> Handle(long authKeyId, TLBytes q)
    {
        return ValueTask.FromResult<TLDataJSON>(DataJSON.Builder()
            .Data(EmptyConfig)
            .Build());
    }
}
