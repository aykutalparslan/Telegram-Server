// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.TL;

/// <summary>
/// Declares the layer and constructor used to dispatch a TL handler or service
/// method. The composition root discovers these declarations and registers the
/// corresponding function under its dispatch key.
/// </summary>
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
