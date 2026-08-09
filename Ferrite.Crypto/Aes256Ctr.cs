// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System;
using System.Buffers;
using System.Security.Cryptography;

namespace Ferrite.Crypto;

// https://stackoverflow.com/a/51188472/2015348
// https://github.com/tdlib/td/blob/master/tdutils/td/utils/crypto.cpp
// original-header
// Copyright Aliaksei Levin (levlam@telegram.org), Arseny Smirnov (arseny30@gmail.com) 2014-2022
//
// Distributed under the Boost Software License, Version 1.0. (See accompanying
// file LICENSE_1_0.txt or copy at http://www.boost.org/LICENSE_1_0.txt)
// original-header

public class Aes256Ctr
{
    //TODO: Can we reuse this somehow?
    private readonly Aes aes;
    private byte[] _counter;
    private byte[] _counterEncrypted;
    private ICryptoTransform? counterEncryptor;
    int _currentPos = 0;

    public Aes256Ctr(byte[] key, byte[] iv)
    {
        if (key.Length != 32 || iv.Length != 16)
        {
            throw new ArgumentOutOfRangeException();
        }
        aes = Aes.Create();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        _counter = iv;
        _counterEncrypted = new byte[16];
        counterEncryptor = aes.CreateEncryptor(key, new byte[16]);
    }

    public void Transform(Span<byte> data)
    {
        if(counterEncryptor == null)
        {
            throw new Exception("Not initialized.");
        }
        for (int i = 0; i < data.Length; i++)
        {
            if (_currentPos == 0)
            {
                counterEncryptor.TransformBlock(
                    _counter, 0, _counter.Length, _counterEncrypted, 0);

                for (var i2 = _counter.Length - 1; i2 >= 0; i2--)
                {
                    if (++_counter[i2] != 0)
                    {
                        break;
                    }
                }
            }
            data[i] = (byte)(data[i] ^ _counterEncrypted[_currentPos]);

            _currentPos = (_currentPos + 1) & 15;
        }
    }
    public void Transform(ReadOnlySequence<byte> from, Span<byte> to)
    {
        if (counterEncryptor == null)
        {
            throw new Exception("Not initialized.");
        }
        SequenceReader<byte> reader = new SequenceReader<byte>(from);
        byte b;
        for (int i = 0; i < Math.Min(from.Length, to.Length); i++)
        {
            if (_currentPos == 0)
            {
                counterEncryptor.TransformBlock(
                    _counter, 0, _counter.Length, _counterEncrypted, 0);

                for (var i2 = _counter.Length - 1; i2 >= 0; i2--)
                {
                    if (++_counter[i2] != 0)
                    {
                        break;
                    }
                }
            }
            reader.TryRead(out b);
            to[i] = (byte)(b ^ _counterEncrypted[_currentPos]);

            _currentPos = (_currentPos + 1) & 15;
        }
    }
    public void TransformPeek(ReadOnlySequence<byte> from, Span<byte> to)
    {
        //transform with the copies of the counter and counterEncrypted and currentPos
        var counter = _counter.ToArray();
        var counterEncrypted = _counterEncrypted.ToArray();
        var currentPos = _currentPos;
        if (counterEncryptor == null)
        {
            throw new Exception("Not initialized.");
        }
        SequenceReader<byte> reader = new SequenceReader<byte>(from);
        byte b;
        for (int i = 0; i < Math.Min(from.Length, to.Length); i++)
        {
            if (currentPos == 0)
            {
                counterEncryptor.TransformBlock(
                    counter, 0, counter.Length, counterEncrypted, 0);

                for (var i2 = counter.Length - 1; i2 >= 0; i2--)
                {
                    if (++counter[i2] != 0)
                    {
                        break;
                    }
                }
            }
            reader.TryRead(out b);
            to[i] = (byte)(b ^ counterEncrypted[currentPos]);

            currentPos = (currentPos + 1) & 15;
        }
    }
}


