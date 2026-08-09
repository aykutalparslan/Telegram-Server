// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Buffers;
using System.IO.Pipelines;
using DotNext.Buffers;
using DotNext.IO;
using DotNext.IO.Pipelines;

namespace Ferrite.Core.Framing;

public class EncoderPipe : IDisposable
{
    private readonly Pipe _encoderPipe;
    private readonly Pipe _pipe;
    private readonly IFrameEncoder _encoder;
    private Task? _encodeTask;
    public EncoderPipe(IFrameEncoder encoder)
    {
        _encoder = encoder;
        _encoderPipe = new Pipe();
        _pipe = new Pipe();
        _encodeTask = DoEncode();
        Input = _pipe.Reader;
    }
    public async ValueTask<FlushResult> WriteLength(int length)
    {
        var header = _encoder.GenerateHead(length);
        foreach (var segment in header)
        {
            _encoderPipe.Writer.Write(segment.Span);
        }

        return await _encoderPipe.Writer.FlushAsync();
    }
    public async ValueTask<FlushResult> WriteAsync(SequenceReader reader)
    {
        int count = (int)reader.RemainingSequence.Length;
        var encBuff = _encoderPipe.Writer.GetMemory(count);
        reader.Read(encBuff.Span.Slice(0, count));
        _encoderPipe.Writer.Advance(count);
        return await _encoderPipe.Writer.FlushAsync();
    }
    public async ValueTask<FlushResult> WriteAsync(byte[] data)
    {
        return await _encoderPipe.Writer.WriteAsync(data);
    }
    public async ValueTask<FlushResult> WriteAsync(Memory<byte> data)
    {
        return await _encoderPipe.Writer.WriteAsync(data);
    }
    public async ValueTask<FlushResult> WriteAsync(ReadOnlySequence<byte> data)
    {
        foreach (var segment in data)
        {
            _encoderPipe.Writer.Write(segment.Span);
        }

        return await _encoderPipe.Writer.FlushAsync();
    }
    private async Task DoEncode()
    {
        while (true)
        {
            var readResult = await _encoderPipe.Reader.ReadAsync();
            var buff = readResult.Buffer;
            var encoded = _encoder.EncodeBlock(buff);
            _pipe.Writer.Write(encoded);
            await _pipe.Writer.FlushAsync();
            _encoderPipe.Reader.AdvanceTo(readResult.Buffer.End, 
                readResult.Buffer.End);

            if (readResult.IsCompleted)
            {
                var tail = _encoder.EncodeTail();
                if (tail.Length > 0)
                {
                    await _pipe.Writer.WriteAsync(tail);
                }
                await _pipe.Writer.CompleteAsync();
                break;
            }
        }
    }

    public async ValueTask CompleteAsync()
    {
        await _encoderPipe.Writer.CompleteAsync();
    }

    public PipeReader Input { get; }

    public void Dispose()
    {
        
    }
}
