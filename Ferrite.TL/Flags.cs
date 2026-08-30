// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System;
using System.Runtime.InteropServices;

namespace Ferrite.TL;

[StructLayout(LayoutKind.Sequential)]
public struct Flags
{
    public Flags(int value)
    {
        _value = unchecked((uint)value);
    }
    private uint _value;
    public readonly int ToInt() => unchecked((int)_value);
    public bool this[int n]
    {
        get => ((_value >> n) & 1u) != 0;
        set
        {
            if (value)
            {
                _value |= 1u << n;
            }
            else
            {
                _value &= ~(1u << n);
            }
        }
    }
}

