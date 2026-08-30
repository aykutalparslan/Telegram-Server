// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.mtproto;

namespace Ferrite.Services.Common;

public class RpcErrorGenerator
{
    public static TLBytes GenerateError(int code, ReadOnlySpan<byte> message)
    {
        var err = RpcError.Builder()
            .ErrorCode(code)
            .ErrorMessage(message)
            .Build();
        return (TLBytes)err.TLBytes!;
    }
}