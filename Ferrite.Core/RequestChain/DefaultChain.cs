// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Core.Execution;
using Ferrite.TL;

namespace Ferrite.Core.RequestChain;

public class DefaultChain: ITLHandler
{
    private readonly ILinkedHandler _first;
    public DefaultChain(AuthKeyProcessor authKeyProcessor, MsgContainerProcessor msgContainerProcessor,
        ServiceMessagesProcessor serviceMessagesProcessor, GZipProcessor gZipProcessor,
        MTProtoRequestProcessor mtProtoRequestProcessor)
    {
        _first = authKeyProcessor;
        _first.SetNext(msgContainerProcessor)
            .SetNext(gZipProcessor)
            .SetNext(serviceMessagesProcessor)
            .SetNext(mtProtoRequestProcessor);
    }

    public async ValueTask Process(object? sender, TLBytes input, TLExecutionContext ctx)
    {
        await _first.Process(sender, input, ctx);
    }

    public async ValueTask Process(object? sender, ITLStreamingObject input, TLExecutionContext ctx)
    {
        await _first.Process(sender, input, ctx);
    }
}
