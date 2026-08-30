// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.TL;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class TLFunctionAttribute : Attribute
{
    public const int DefaultLayer = 214;

    public int Layer { get; }
    public int Constructor { get; }

    public TLFunctionAttribute(int constructor, int layer = DefaultLayer)
    {
        Constructor = constructor;
        Layer = layer;
    }
}
