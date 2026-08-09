// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System;
using System.Numerics;

namespace Ferrite.Crypto;

public interface IRandomGenerator
{
    public int GetRandomPrime();
    public int GetRandomNumber(int toExclusive);
    public int GetRandomNumber(int fromInclusive, int toExclusive);
    public long NextLong();
    public byte[] GetRandomBytes(int count);
    public BigInteger GetRandomInteger(BigInteger min, BigInteger max);
    public int GetNext(int fromInclusive, int toExclusive);
    public void Fill(Span<byte> data);
}

