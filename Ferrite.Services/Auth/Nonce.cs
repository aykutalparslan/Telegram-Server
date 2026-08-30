// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System;
using xxHash;

namespace Ferrite.Services.Auth;

public struct Nonce : IEquatable<Nonce>
{
    private byte[] _value;
    public Nonce()
    {
        _value = new byte[16];
    }
    public Nonce(byte[] val)
    {
        if (val.Length == 16)
        {
            _value = val;
        }
        else
        {
            throw new ArgumentOutOfRangeException();
        }
    }

    public static implicit operator byte[](Nonce i)
    {
        return i._value;
    }
    public static explicit operator Nonce(byte[] b) => new Nonce(b);

    public Span<byte> AsSpan()
    {
        return _value;
    }
   
    public static bool operator ==(Nonce left, Nonce right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Nonce left, Nonce right)
    {
        return !(left == right);
    }

    public override int GetHashCode()
    {
        return (int)AsSpan().GetXxHash32();
    }

    public bool Equals(Nonce other)
    {
        return _value.SequenceEqual(other._value);
    }

    public override bool Equals(object? obj)
    {
        return obj is Nonce && Equals((Nonce)obj);
    }
}

