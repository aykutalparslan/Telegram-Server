// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Data;

public class ConcatenatedStream: Stream
{
    private int _position;
    private readonly Queue<Stream> _streams;
    private Stream? _currentStream;
    private int _currentStreamPosition;
    private int _offset;
    private readonly int _limit;
    public ConcatenatedStream(Queue<Stream> streams, int offset, int limit)
    {
        _streams = streams;
        foreach (var stream in streams)
        {
            Length += stream.Length;
        }
        _offset = offset;
        _limit = limit;
        Length = Math.Max(0, Math.Min(Length - _offset, _limit));
    }
    public override void Flush()
    {
        throw new NotImplementedException();
    }
    public override int Read(byte[] buffer, int offset, int count)
    {
        SetCurrentStream();
        Discard();
        if (_currentStream == null)
        {
            return 0;
        }
        int read = (int)Math.Min(count, _currentStream.Length - _currentStreamPosition);
        read = Math.Min(read, _limit - _position);
        if (read == 0)
        {
            return 0;
        }
        read = _currentStream.Read(buffer, offset, read);
        _currentStreamPosition += read;
        _position += read;
        if (_streams.Count == 0 &&
            _currentStreamPosition == _currentStream.Length)
        {
            _currentStream.Dispose();
            _currentStream = null;
        }

        return read;
    }

    private void Discard()
    {
        while (_offset > 0 && _currentStream != null)
        {
            int toBeDiscarded = (int)Math.Min(_offset, _currentStream.Length - _currentStreamPosition);
            var discard = new byte[toBeDiscarded];
            _currentStream.Read(discard, 0, toBeDiscarded);
            _currentStreamPosition += toBeDiscarded;
            _offset -= toBeDiscarded;
            SetCurrentStream();
        }
    }

    private void SetCurrentStream()
    {
        if (_currentStream == null && _streams.Count > 0)
        {
            _currentStream = _streams.Dequeue();
            _currentStreamPosition = 0;
        }
        else if (_currentStream != null && 
                 _currentStreamPosition == _currentStream.Length)
        {
            _currentStream.Dispose();
            _currentStream = null;
            _currentStreamPosition = 0;
            if (_streams.Count > 0)
            {
                _currentStream = _streams.Dequeue();
            }
        }
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

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length { get; }
    public override long Position { get => _position; 
        set => throw new NotSupportedException(); }

    public override void Close()
    {
        if (_currentStream != null)
        {
            _currentStream.Dispose();
        }
        while (_streams.Count > 0)
        {
            var stream = _streams.Dequeue();
            stream.Dispose();
        }
        base.Close();
    }

    public override ValueTask DisposeAsync()
    {
        _currentStream?.Dispose();
        while (_streams.Count > 0)
        {
            var stream = _streams.Dequeue();
            stream.Dispose();
        }
        base.Close();
        return base.DisposeAsync();
    }
}
