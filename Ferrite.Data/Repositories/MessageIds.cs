// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;

namespace Ferrite.Data.Repositories;

public static class MessageIds
{
    /// <summary>
    /// Reads the message id from any stored Message variant. The message,
    /// messageService, and messageEmpty constructors have different layouts,
    /// so the id must be read through the matching concrete view.
    /// </summary>
    public static int GetId(in TLMessage message) => message.Type switch
    {
        TLMessage.MessageType.Message => message.AsMessage().Id,
        TLMessage.MessageType.MessageService => message.AsMessageService().Id,
        TLMessage.MessageType.MessageEmpty => message.AsMessageEmpty().Id,
        _ => 0
    };
}
