// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;

namespace Ferrite.Services.Handlers.UploadMethods;

public sealed class GetFileHashesHandler
{
    [TLFunction(Constructors.baseLayer_GetFileHashes)]
    public ValueTask<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var hashes = new Vector();
        byte[] bytes = hashes.ToReadOnlySpan().ToArray();
        return ValueTask.FromResult(new TLBytes(bytes, 0, bytes.Length));
    }
}
