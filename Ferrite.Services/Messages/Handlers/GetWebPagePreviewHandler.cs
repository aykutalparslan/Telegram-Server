// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

// Server-side URL fetching and web-page caching are deferred. Return an empty
// media preview so clients can continue without a fabricated remote fetch.
public sealed class GetWebPagePreviewHandler
{
    [TLFunction(Constructors.baseLayer_GetWebPagePreview)]
    public ValueTask<TLWebPagePreview> Handle(long authKeyId, TLBytes q)
    {
        using TLMessageMedia media = MessageMediaEmpty.Builder().Build();
        TLWebPagePreview result = WebPagePreview.Builder()
            .Media(media.AsSpan())
            .Chats(new Vector())
            .Users(new Vector())
            .Build();
        return ValueTask.FromResult(result);
    }
}
