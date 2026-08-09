// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.IO.Pipelines;

namespace Ferrite.Utils;

public class TLBytesStream : Stream
{
    private readonly Stream _pipeStream;
    private int _remaining;

    public TLBytesStream(PipeReader reader, int count)
    {
        _pipeStream = reader.AsStream();
        _remaining = count;
        Length = count;
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length { get; }
    public override long Position
    {
        get => 0;
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
        throw new NotSupportedException();
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        int toBeCopied = Math.Min(_remaining, count);
        if (toBeCopied == 0)
        {
            return 0;
        }

        int copied = _pipeStream.Read(buffer, offset, toBeCopied);
        _remaining -= copied;
        return copied;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        throw new NotSupportedException();
    }

    public override void SetLength(long value)
    {
        throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        throw new NotSupportedException();
    }

    public override void Close()
    {
        _pipeStream.Close();
        base.Close();
    }
}
