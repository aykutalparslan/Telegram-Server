// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.mtproto;

namespace Ferrite.Core.Execution.Functions;

public class RpcResultGenerator
{
    public static TLBytes Generate(TLBytes result, long reqMessageId)
    {
        return (TLBytes)RpcResult.Builder()
            .ReqMsgId(reqMessageId)
            .Result(result.AsSpan())
            .Build()
            .TLBytes!;
    }
}