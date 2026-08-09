// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Buffers;
namespace Ferrite.Data;

/// <summary>
/// Sequentially reads a bounded file range while keeping only one remote
/// object-part response open at a time.
/// </summary>
internal sealed class ObjectStorePartStream : Stream
{
    private readonly IObjectStore _store;
    private readonly long _fileId;
    private readonly int _parts;
    private readonly bool _isBigFile;
    private int _part;
    private int _partOffset;
    private Stream? _current;
    private long _position;
    private bool _disposed;

    public ObjectStorePartStream(IObjectStore store, long fileId, int parts,
        int partSize, bool isBigFile, long offset, long length)
    {
        _store = store;
        _fileId = fileId;
        _parts = parts;
        _isBigFile = isBigFile;
        _part = checked((int)(offset / partSize));
        _partOffset = checked((int)(offset % partSize));
        Length = length;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (buffer.Length - offset < count) throw new ArgumentException(
            "Offset and count exceed the buffer length.");

        int total = 0;
        while (total < count && _position < Length)
        {
            EnsureCurrent();
            if (_current == null) break;
            DiscardOffset();
            int wanted = checked((int)Math.Min(count - total, Length - _position));
            int read = _current.Read(buffer, offset + total, wanted);
            if (read == 0)
            {
                AdvancePart();
                continue;
            }
            total += read;
            _position += read;
            if (_position == Length) AdvancePart();
        }
        return total;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        int total = 0;
        while (total < buffer.Length && _position < Length)
        {
            await EnsureCurrentAsync();
            if (_current == null) break;
            await DiscardOffsetAsync(cancellationToken);
            int wanted = checked((int)Math.Min(buffer.Length - total,
                Length - _position));
            int read = await _current.ReadAsync(buffer.Slice(total, wanted),
                cancellationToken);
            if (read == 0)
            {
                await AdvancePartAsync();
                continue;
            }
            total += read;
            _position += read;
            if (_position == Length) await AdvancePartAsync();
        }
        return total;
    }

    private void EnsureCurrent()
    {
        if (_current != null || _part >= _parts) return;
        _current = GetPart(_part).AsTask().GetAwaiter().GetResult();
    }

    private async ValueTask EnsureCurrentAsync()
    {
        if (_current != null || _part >= _parts) return;
        _current = await GetPart(_part);
    }

    private void DiscardOffset()
    {
        if (_partOffset == 0 || _current == null) return;
        byte[] scratch = ArrayPool<byte>.Shared.Rent(
            Math.Min(_partOffset, 8192));
        try
        {
            while (_partOffset > 0)
            {
                int read = _current.Read(scratch, 0,
                    Math.Min(_partOffset, scratch.Length));
                if (read == 0) break;
                _partOffset -= read;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(scratch);
        }
    }

    private async ValueTask DiscardOffsetAsync(CancellationToken cancellationToken)
    {
        if (_partOffset == 0 || _current == null) return;
        byte[] scratch = ArrayPool<byte>.Shared.Rent(
            Math.Min(_partOffset, 8192));
        try
        {
            while (_partOffset > 0)
            {
                int read = await _current.ReadAsync(
                    scratch.AsMemory(0, Math.Min(_partOffset, scratch.Length)),
                    cancellationToken);
                if (read == 0) break;
                _partOffset -= read;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(scratch);
        }
    }

    private ValueTask<Stream> GetPart(int part) => _isBigFile
        ? _store.GetBigFilePart(_fileId, part)
        : _store.GetFilePart(_fileId, part);

    private void AdvancePart()
    {
        _current?.Dispose();
        _current = null;
        _part++;
        _partOffset = 0;
    }

    private async ValueTask AdvancePartAsync()
    {
        if (_current != null) await _current.DisposeAsync();
        _current = null;
        _part++;
        _partOffset = 0;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _current?.Dispose();
            _current = null;
        }
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            if (_current != null) await _current.DisposeAsync();
            _current = null;
        }
        GC.SuppressFinalize(this);
    }

    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) =>
        throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) =>
        throw new NotSupportedException();
    public override bool CanRead => !_disposed;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length { get; }
    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException();
    }
}
