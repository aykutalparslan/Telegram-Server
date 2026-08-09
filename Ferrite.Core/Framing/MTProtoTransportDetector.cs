// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Buffers;
using Autofac;
using DotNext.Buffers;
using Ferrite.Crypto;
using Ferrite.Services;

namespace Ferrite.Core.Framing;

public class MTProtoTransportDetector : ITransportDetector
{
    const byte Abridged = 0xef;
    const int AbridgedInt = unchecked((int)0xefefefef);
    const int Intermediate = unchecked((int)0xeeeeeeee);
    const int PaddedIntermediate = unchecked((int)0xdddddddd);
    const int Full = unchecked((int)0xdddddddd);
    const int WebSocketGet = 542393671;
    private readonly ILifetimeScope _scope;

    public MTProtoTransportDetector(ILifetimeScope scope)
    {
        _scope = scope;
    }

    public MTProtoTransport DetectTransport(ReadOnlySequence<byte> bytes,
        out IFrameDecoder? decoder, out IFrameEncoder? encoder, 
        out SequencePosition sequencePosition)
    {
        var reader = new SequenceReader<byte>(bytes);
        MTProtoTransport transport = MTProtoTransport.Unknown;
        decoder = null;
        encoder = null;
        if (reader.Remaining > 0)
        {
            reader.TryPeek(out var firstbyte);
            if (firstbyte == Abridged)
            {
                transport = MTProtoTransport.Abridged;
                reader.Advance(1);
                decoder = new AbridgedFrameDecoder(_scope.Resolve<IMTProtoService>());
                encoder = new AbridgedFrameEncoder();
                sequencePosition = reader.Position;
                return transport;
            }
        }

        if (reader.Remaining > 3)
        {
            reader.TryReadLittleEndian(out int firstint);
            if (firstint == Intermediate)
            {
                transport = MTProtoTransport.Intermediate;
                decoder = new IntermediateFrameDecoder(_scope.Resolve<IMTProtoService>());
                encoder = new IntermediateFrameEncoder();
                sequencePosition = reader.Position;
                return transport;
            }
            else if (firstint == PaddedIntermediate)
            {
                transport = MTProtoTransport.PaddedIntermediate;
                decoder = new PaddedIntermediateFrameDecoder(_scope.Resolve<IMTProtoService>());
                encoder = new PaddedIntermediateFrameEncoder();
                sequencePosition = reader.Position;
                return transport;
            }
        }

        if (reader.Remaining > 3)
        {
            reader.TryReadLittleEndian(out int secondint);
            if (secondint == Full)
            {
                transport = MTProtoTransport.Full;
                reader.Rewind(8);
                decoder = new FullFrameDecoder(_scope.Resolve<IMTProtoService>());
                encoder = new FullFrameEncoder();
                sequencePosition = reader.Position;
                return transport;
            }
        }
        else
        {
            reader.Rewind(4);
            sequencePosition = reader.Position;
            return transport;
        }

        if (reader.Remaining > 55)
        {
            var payload = reader.Sequence.Slice(0, 64);
            var decryptionKey = payload.Slice(8, 32).ToArray();
            var decryptionIV = payload.Slice(40, 16).ToArray();
            var encryptionKey = payload.ToArray().Reverse().ToArray().AsSpan().Slice(8, 32).ToArray();
            var encryptionIV = payload.ToArray().Reverse().ToArray().AsSpan().Slice(40, 16).ToArray();

            Aes256Ctr decryptor = new Aes256Ctr(decryptionKey, decryptionIV);
            Aes256Ctr encryptor = new Aes256Ctr(encryptionKey, encryptionIV);

            using (IMemoryOwner<byte> owner = UnmanagedMemoryPool<byte>.Shared.Rent(64))
            {
                Memory<byte> data = owner.Memory;
                decryptor.Transform(payload, data.Span);
                SpanReader<byte> rd = new SpanReader<byte>(data.Span.Slice(56));
                int identifier = rd.ReadInt32(true);
                if (identifier == AbridgedInt)
                {
                    transport = MTProtoTransport.Abridged;
                    decoder = new AbridgedFrameDecoder(decryptor, _scope.Resolve<IMTProtoService>());
                    encoder = new AbridgedFrameEncoder(encryptor);
                }
                else if (identifier == Intermediate)
                {
                    transport = MTProtoTransport.Intermediate;
                    decoder = new IntermediateFrameDecoder(decryptor, _scope.Resolve<IMTProtoService>());
                    encoder = new IntermediateFrameEncoder(encryptor);
                }
                else if (identifier == PaddedIntermediate)
                {
                    transport = MTProtoTransport.PaddedIntermediate;
                    decoder = new PaddedIntermediateFrameDecoder(decryptor, _scope.Resolve<IMTProtoService>());
                    encoder = new PaddedIntermediateFrameEncoder(encryptor);
                }
                else
                {
                    transport = MTProtoTransport.Full;
                    decoder = new FullFrameDecoder(decryptor, _scope.Resolve<IMTProtoService>());
                    encoder = new FullFrameEncoder(encryptor);
                }
            }

            reader.Advance(56);
            sequencePosition = reader.Position;
            return transport;
        }
        else
        {
            reader.Rewind(8);
            sequencePosition = reader.Position;
            return transport;
        }
    }
}

