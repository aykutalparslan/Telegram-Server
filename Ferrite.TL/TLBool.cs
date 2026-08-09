// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Buffers;
using System.Runtime.InteropServices;
using Ferrite.TL.mtproto;

namespace Ferrite.TL;

public readonly struct TLBool : IDisposable
{
    private readonly TLBytes _tlBytes;
    private readonly int _constructor;

    public TLBool(IMemoryOwner<byte> memoryOwner, int offset, int length)
    {
        _constructor = MemoryMarshal.Read<int>(memoryOwner.Memory.Span[offset..]);
        ThrowIfInvalid();
        _tlBytes = new TLBytes(memoryOwner, offset, length);
    }

    public TLBool(Memory<byte> memory, int offset, int length)
    {
        _constructor = MemoryMarshal.Read<int>(memory.Span[offset..]);
        ThrowIfInvalid();
        _tlBytes = new TLBytes(memory, offset, length);
    }

    private TLBool(TLBytes bytes)
    {
        _constructor = bytes.Constructor;
        ThrowIfInvalid();
        _tlBytes = bytes;
    }
    
    private void ThrowIfInvalid()
    {
        if (_constructor != unchecked((int)0x997275b5) &&
            _constructor != unchecked((int)0xbc799737) &&
            _constructor != unchecked((int)0x2144ca19))
        {
            throw new InvalidCastException();
        }
    }

    public int Constructor => _tlBytes.Constructor;
    
    public TLBytes TLBytes => _tlBytes;

    public static explicit operator TLBool(TLBytes b) => new (b);
    
    public static implicit operator TLBytes(TLBool b) => b._tlBytes;
    
    public BoolTrue AsBoolTrue() => (BoolTrue)_tlBytes.AsSpan();
    
    public BoolFalse AsBoolFalse() => (BoolFalse)_tlBytes.AsSpan();
    
    public RpcError AsRpcError() => (RpcError)_tlBytes.AsSpan();
    
    public BoolType Type => _constructor switch
    {
        unchecked((int)0x997275b5) => BoolType.True,
        unchecked((int)0xbc799737) => BoolType.False,
        unchecked((int)0x2144ca19) => BoolType.RpcError,
        _ => BoolType.InvalidObject
    };

    public Span<byte> AsSpan() => _tlBytes.AsSpan();

    public enum BoolType
    {
        True,
        False,
        RpcError,
        InvalidObject,
    }

    public void Dispose()
    {
        _tlBytes.Dispose();
    }
}