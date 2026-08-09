// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Buffers;
using System.Buffers.Binary;
using DotNext.Buffers;
using DotNext.IO;
using Ferrite.Crypto;
using Ferrite.Services;
using Ferrite.TL;

namespace Ferrite.Core.Framing;

public abstract class FrameDecoderBase : IFrameDecoder
{
    private const int StreamChunkSize = 1024;
    protected readonly byte[] LengthBytes = new byte[4];
    protected int Length;
    /// <summary>
    /// Number of length bytes to be skipped at the beginning of the frame before decoding it.
    /// </summary>
    protected int LengthBytesToSkip;
    /// <summary>
    /// Number of bytes to be skipped at the beginning of the frame.
    /// </summary>
    protected int Header;
    /// <summary>
    /// Number of bytes to be skipped at the end of the frame.
    /// </summary>
    protected int Tail;
    private int _remaining;
    private bool _isStream;
    protected readonly Aes256Ctr? Decryptor;
    private readonly IMTProtoService _mtproto;
    private readonly byte[] _headerBytes = new byte[72];

    protected FrameDecoderBase(IMTProtoService mtproto)
    {
        _mtproto = mtproto;
    }

    protected FrameDecoderBase(Aes256Ctr decryptor, IMTProtoService mtproto)
    {
        Decryptor = decryptor;
        _mtproto = mtproto;
    }

    public bool Decode(ReadOnlySequence<byte> bytes, out ReadOnlySequence<byte> frame,
        out bool isStream, out bool requiresQuickAck, out SequencePosition position)
    {
        var reader = new SequenceReader<byte>(bytes);
        isStream = _isStream;
        requiresQuickAck = false;
        if (Length == 0)
        {
            requiresQuickAck = DecodeLength(ref reader, out var emptyFrame);
            if (emptyFrame) return EmptyFrame(out frame, out position, reader);
            _remaining = Length;
        }
        
        if (reader.Remaining >= 72 && !_isStream)
        {
            _isStream = CheckIfStream(reader.UnreadSequence.Slice(0, 72));
            isStream = _isStream;
        }
        
        int toBeWritten = Math.Min(_remaining, StreamChunkSize);
        if (_isStream && reader.Remaining >= toBeWritten)
        {
            return HandleStream(out frame, out position, reader, toBeWritten);
        }
        if (reader.Remaining < Length)
        {
            frame = new ReadOnlySequence<byte>();
            position = reader.Position;
            return false;
        }
        return HandleFrame(out frame, out position, reader);
    }

    /// <summary>
    /// Decodes the frame length.
    /// </summary>
    /// <param name="reader">Reader for the buffer.</param>
    /// <param name="emptyFrame">Is set if the received data length is too short.</param>
    /// <returns>If a quick ack is required.</returns>
    protected abstract bool DecodeLength(ref SequenceReader<byte> reader, out bool emptyFrame);

    protected bool CheckRequiresQuickAck(byte[] arr, int pos)
    {
        bool requiresQuickAck = false;
        if ((arr[pos] & 1 << 7) != 1 << 7) return requiresQuickAck;
        requiresQuickAck = true;
        arr[pos] &= 0x7f;

        return requiresQuickAck;
    }

    private static bool EmptyFrame(out ReadOnlySequence<byte> frame, out SequencePosition position, SequenceReader<byte> reader)
    {
        frame = new ReadOnlySequence<byte>();
        position = reader.Position;
        return false;
    }

    private bool HandleStream(out ReadOnlySequence<byte> frame, out SequencePosition position, SequenceReader<byte> reader,
        int toBeWritten)
    {
        int head = _remaining == Length ? LengthBytesToSkip + Header : 0;
        ReadOnlySequence<byte> chunk = reader.UnreadSequence.Slice(0, toBeWritten);
        reader.Advance(toBeWritten);
        _remaining -= toBeWritten;
        int tail = _remaining == 0 ? Tail : 0;
        if (Decryptor != null)
        {
            var chunkDecrypted = new byte[toBeWritten];
            Decryptor.Transform(chunk, _remaining == Length ? 
                chunkDecrypted.AsSpan()[LengthBytesToSkip..] : chunkDecrypted);
            
            frame = new ReadOnlySequence<byte>(chunkDecrypted[head..^tail]);
        }
        else
        {
            frame = chunk;
        }

        if (_remaining == 0)
        {
            Length = 0;
            _isStream = false;
            Array.Clear(LengthBytes);
            position = reader.Position;
            return false;
        }

        position = reader.Position;
        return true;
    }

    private bool HandleFrame(out ReadOnlySequence<byte> frame, out SequencePosition position, SequenceReader<byte> reader)
    {
        ReadOnlySequence<byte> data = reader.UnreadSequence.Slice(0, Length);
        reader.Advance(Length);
        _remaining -= (int)data.Length;
        Length = 0;
        Array.Clear(LengthBytes);
        if (Decryptor != null)
        {
            var frameDecrypted = new byte[data.Length];
            Decryptor.Transform(data, frameDecrypted.AsSpan()[LengthBytesToSkip..]);
            frame = new ReadOnlySequence<byte>(frameDecrypted[(LengthBytesToSkip + Header)..^Tail]);
        }
        else
        {
            frame = data;
        }

        position = reader.Position;
        return reader.UnreadSequence.Length != 0;
    }
    private bool CheckIfStream(ReadOnlySequence<byte> header)
    {
        if (Decryptor != null)
        {
            Decryptor.TransformPeek(header, _headerBytes);
        }
        else
        {
            header.CopyTo(_headerBytes);
        }

        var authKeyId = BinaryPrimitives.ReadInt64LittleEndian(_headerBytes);
        var authKey = (_mtproto.GetAuthKey(authKeyId) ?? 
                       _mtproto.GetTempAuthKey(authKeyId));
        if (authKey is not { Length: > 0 }) return false;
        Span<byte> messageKey = _headerBytes.AsSpan(8,16);
        AesIge aesIge = new(authKey, messageKey);
        Span<byte> messageHeader = _headerBytes.AsSpan(24, 48);
        aesIge.Decrypt(messageHeader);
        var messageData = new TLBytes(_headerBytes.AsMemory(56, 4), 0, 4);
        int constructor = messageData.Constructor;
        return constructor == Constructors.baseLayer_SaveFilePart ||
               constructor == Constructors.baseLayer_SaveBigFilePart;
    }
}
