// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.Schema;

namespace Ferrite.Core.Execution;

public static class LayerRequestNormalizer
{
    public static TLBytes? Normalize(TLBytes source, int layer) =>
        PublishedRequestMappers.TryCreate(layer, out LayerRequestMapper mapper)
            ? Transformed(mapper, source.AsSpan().ToArray())
            : null;

    public static TLBytes? NormalizeOlderMethod(TLBytes source, int layer)
    {
        if (LayerSchemaCatalog.Create(layer).TryGetMethod(source.Constructor, out _))
        {
            return null;
        }

        byte[] request = source.AsSpan().ToArray();
        foreach (int older in LayerSchemaCatalog.Layers.Where(x => x < layer).Reverse())
        {
            if (PublishedRequestMappers.TryCreate(older, out LayerRequestMapper mapper) &&
                Transformed(mapper, request) is { } mapped)
            {
                return mapped;
            }
        }
        return null;
    }

    private static TLBytes? Transformed(LayerRequestMapper mapper, byte[] request)
    {
        LayerTransformResult mapped = mapper.Map(request);
        return mapped.Status == LayerTransformStatus.Transformed
            ? new TLBytes(mapped.Data!, 0, mapped.Data!.Length)
            : null;
    }
}
