// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Core.Execution;
using Ferrite.TL;

namespace Ferrite.Core.RequestChain;

public class GZipProcessor : ILinkedHandler
{
    public ILinkedHandler SetNext(ILinkedHandler value)
    {
        Next = value;
        return Next;
    }

    public ILinkedHandler? Next { get; set; }

    public async ValueTask Process(object? sender, TLBytes input, TLExecutionContext ctx)
    {
        if (input.Constructor == Constructors.mtproto_GzipPacked)
        {
            TLBytes unpacked;
            try
            {
                unpacked = GzipPackedHelper.Unpack(input);
            }
            finally
            {
                input.Dispose();
            }
            if (Next != null) await Next.Process(sender, unpacked, ctx);
            else unpacked.Dispose();
        }
        else if (Next != null) await Next.Process(sender, input, ctx);
        else input.Dispose();
    }

    public async ValueTask Process(object? sender, ITLStreamingObject input, TLExecutionContext ctx)
    {
        if (Next != null) await Next.Process(sender, input, ctx);
    }
}
