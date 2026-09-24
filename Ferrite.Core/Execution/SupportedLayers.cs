// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Collections.Frozen;
using Ferrite.TL.Schema;

namespace Ferrite.Core.Execution;

public static class SupportedLayers
{
    public const int Base = LayerSchemaCatalog.BaseLayer;
    public static int Floor => LayerSchemaCatalog.Layers[0];
    public static IReadOnlySet<int> All { get; } = LayerSchemaCatalog.Layers.ToFrozenSet();

    public static bool Contains(int layer) => All.Contains(layer);
}
