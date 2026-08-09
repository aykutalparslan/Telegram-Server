// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;

namespace Ferrite.Services.Handlers.ChannelForums;

internal static class ChannelForumErrors
{
    internal static Ferrite.TL.baseLayer.TLUpdates Updates(ReadOnlySpan<byte> message) =>
        (Ferrite.TL.baseLayer.TLUpdates)RpcErrorGenerator.GenerateError(400, message);

    internal static Ferrite.TL.baseLayer.messages.TLAffectedHistory AffectedHistory(
        ReadOnlySpan<byte> message) =>
        (Ferrite.TL.baseLayer.messages.TLAffectedHistory)RpcErrorGenerator.GenerateError(
            400, message);

    internal static Ferrite.TL.baseLayer.messages.TLForumTopics ForumTopics(
        ReadOnlySpan<byte> message) =>
        (Ferrite.TL.baseLayer.messages.TLForumTopics)RpcErrorGenerator.GenerateError(
            400, message);
}
