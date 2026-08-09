// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Buffers;
using System.IO.Pipelines;
using System.Security.Cryptography;
using DotNext.Buffers;
using DotNext.IO;
using DotNext.IO.Pipelines;
using Ferrite.Crypto;

namespace Ferrite.Core.Connection;

public class MTProtoPipe : IAsyncDisposable
{
    private readonly Pipe _encryptedPipe;
    private readonly Pipe _pipe;
    private readonly Aes _aes;
    private readonly byte[] _aesKey;
    private readonly byte[] _aesIV;
    private readonly IMemoryOwner<byte> _buff;
    private readonly bool _encrypt;
    private readonly Task? _decryptTask;
    public MTProtoPipe(Span<byte> aesKey, Span<byte> aesIV, bool encrypt)
    {
        _buff = UnmanagedMemory.Allocate<byte>(16);
        _aesKey = new byte[32];
        _aesIV = new byte[32];
        aesKey.CopyTo(_aesKey);
        aesIV.CopyTo(_aesIV);
        _encryptedPipe = new Pipe();
        _pipe = new Pipe();
        _aes = Aes.Create();
        _aes.Key = _aesKey;
        _encrypt = encrypt;
        _decryptTask = DoDecrypt();

        Input = _pipe.Reader;
    }
    public async ValueTask<FlushResult> WriteAsync(SequenceReader reader)
    {
        int count = (int)reader.RemainingSequence.Length;
        var encBuff = _encryptedPipe.Writer.GetMemory(count);
        reader.Read(encBuff.Span.Slice(0, count));
        _encryptedPipe.Writer.Advance(count);
        return await _encryptedPipe.Writer.FlushAsync();
    }
    public void Write(SequenceReader reader)
    {
        int count = (int)reader.RemainingSequence.Length;
        var encBuff = _encryptedPipe.Writer.GetMemory(count);
        reader.Read(encBuff.Span[..count]);
        _encryptedPipe.Writer.Advance(count);
    }
    public async ValueTask<FlushResult> WriteAsync(byte[] data)
    {
        return await _encryptedPipe.Writer.WriteAsync(data);
    }
    public async ValueTask<FlushResult> WriteAsync(ReadOnlySequence<byte> data)
    {
        foreach (var segment in data)
        {
            _encryptedPipe.Writer.Write(segment.Span);
        }

        return await _encryptedPipe.Writer.FlushAsync();
    }
    public async ValueTask<FlushResult> WriteAsync(Memory<byte> data)
    {
        return await _encryptedPipe.Writer.WriteAsync(data);
    }
    private async Task DoDecrypt()
    {
        while (true)
        {
            var readResult = await _encryptedPipe.Reader.ReadAsync();

            var pCount = readResult.Buffer.Length / 16;
            for (int i = 0; i < pCount; i++)
            {
                var slice = readResult.Buffer.Slice(i*16,16);
                slice.CopyTo(_buff.Memory.Span);
                if (_encrypt)
                {
                    _aes.EncryptIge(_buff.Memory.Span, _aesIV);
                }
                else
                {
                    _aes.DecryptIge(_buff.Memory.Span, _aesIV);
                }

                var buffer = _pipe.Writer.GetMemory(16);
                _buff.Memory.CopyTo(buffer);
                _pipe.Writer.Advance(16);
            }
            await _pipe.Writer.FlushAsync();
            _encryptedPipe.Reader.AdvanceTo(readResult.Buffer.Slice(0,pCount*16).End);

            if (readResult.IsCompleted ||
                readResult.IsCanceled)
            {
                break;
            }
        }
        await _pipe.Writer.CompleteAsync();
    }

    public void Complete()
    {
        _encryptedPipe.Writer.Complete();
    }

    public async ValueTask CompleteAsync()
    {
        await _encryptedPipe.Writer.CompleteAsync();
    }

    public PipeReader Input { get; }

    public async ValueTask DisposeAsync()
    {
        if (_decryptTask != null) await _decryptTask;
        _buff.Dispose();
    }
}
