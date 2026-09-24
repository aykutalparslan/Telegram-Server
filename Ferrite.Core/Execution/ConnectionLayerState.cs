// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Core.Execution;

public abstract record ConnectionLayerResolution
{
    public sealed record Unknown : ConnectionLayerResolution;
    public sealed record Resolved(int Layer) : ConnectionLayerResolution;
}

public sealed class ConnectionLayerState
{
    private ConnectionLayerResolution _value = new ConnectionLayerResolution.Unknown();

    public ConnectionLayerResolution Value => Volatile.Read(ref _value);

    public void Resolve(int layer)
    {
        if (!SupportedLayers.Contains(layer))
        {
            throw new ArgumentOutOfRangeException(nameof(layer));
        }
        Volatile.Write(ref _value, new ConnectionLayerResolution.Resolved(layer));
    }
}
