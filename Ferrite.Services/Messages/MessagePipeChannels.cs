// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Messages;

public static class MessagePipeChannels
{
    private const string CodecVersion = "tl2-";

    public static string ForNode(Guid nodeId) => CodecVersion + nodeId;
}
