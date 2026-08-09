// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL;
using Ferrite.TL.baseLayer;

namespace Ferrite.Services.Phone.Handlers;

/// <summary>
/// Returns the call configuration JSON. TDLib's CallActor requests this on
/// every call start and gates callStateReady on a valid response, so it must
/// always return schema-valid JSON. Ferrite has no tunable call config yet,
/// so the payload is an empty JSON object; it is expanded only when a current
/// tgcalls client proves a key is required.
/// </summary>
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
