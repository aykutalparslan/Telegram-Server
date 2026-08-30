// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System;
namespace Ferrite.Data.Models;

public enum InputPeerType
{
    Empty,
    Self,
    Chat,
    User,
    Channel,
    UserFromMessage,
    ChannelFromMessage
}

