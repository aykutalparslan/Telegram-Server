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
    // Some schemas use the flags word as a value in its own right rather than
    // only as presence bits — tde2e's e2e.chainGroupParticipant stores its
    // permission mask there, and bits outside the known set are load-bearing
    // because the reference rejects a group state that sets them.
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

