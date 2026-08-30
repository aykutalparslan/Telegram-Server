// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC


namespace Ferrite.Services.Media;

internal sealed class BufferedFileOwner : IFileOwner
{
    private readonly ReadOnlyMemory<byte> _bytes;

    public BufferedFileOwner(ReadOnlyMemory<byte> bytes, long reqMsgId,
        byte[] streamHeader)
    {
        _bytes = bytes;
        ReqMsgId = reqMsgId;
        TLObjectHeader = streamHeader;
    }

    public byte[] TLObjectHeader { get; init; }

    public ValueTask<Stream> GetFileStream() => ValueTask.FromResult<Stream>(
        new MemoryStream(_bytes.ToArray(), writable: false));

    public long ReqMsgId { get; }
}
