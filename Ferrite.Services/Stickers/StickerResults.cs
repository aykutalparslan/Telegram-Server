// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL;
using Ferrite.TL.baseLayer;

namespace Ferrite.Services.Stickers;

internal static class StickerResults
{
    public static TLBytes True()
    {
        TLBool result = BoolTrue.Builder().Build();
        return result.TLBytes;
    }

    public static TLBytes False()
    {
        TLBool result = BoolFalse.Builder().Build();
        return result.TLBytes;
    }

    public static TLBytes Error(string message) =>
        RpcErrorGenerator.GenerateError(400, Encoding.UTF8.GetBytes(message));

    public static TLBytes StorageError() =>
        RpcErrorGenerator.GenerateError(500, "STORAGE_FAILED"u8);
}
