// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class GetSavedDialogsByIDHandler
{
    [TLFunction(Constructors.baseLayer_GetSavedDialogsByID)]
    public ValueTask<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var result = SavedDialogs.Builder()
            .Dialogs(new Vector())
            .Messages(new Vector())
            .Chats(new Vector())
            .Users(new Vector())
            .Build();
        return ValueTask.FromResult(result.TLBytes!.Value);
    }
}
