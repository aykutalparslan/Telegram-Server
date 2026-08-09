// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System;
using System.Collections;

namespace Ferrite.Utils;

public class CircularQueue<T> : IEnumerable<T> where T : unmanaged
{
    private T[] _array;
    private int _head = 0;
    private int _tail = 0;
    private int _count = 0;
    public int Count => _count;
    public CircularQueue(int limit)
    {
        _array = new T[limit];
    }
    public void Enqueue(T item)
    {
        if (_array.Length == _count)
        {
            _head = (_head + 1) % _array.Length;
        }
        _array[_tail] = item;
        _tail = (_tail + 1) % _array.Length;
        _count++;
        if (_count > _array.Length)
        {
            _count = _array.Length;
        }
    }
    public T Peek()
    {
        return _array[_head];
    }
    public T Dequeue()
    {
        T t = _array[_head];
        _head = (_head + 1) % _array.Length;
        _count--;
        return t;
    }

    public IEnumerator<T> GetEnumerator()
    {
        for (int i = 0; i < _count; i++)
        {
            yield return _array[(_head + i) % _array.Length];
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}