// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.TL.Schema;

public static class PublishedRequestMappers
{
    private static readonly object Gate = new object();
    private static readonly Dictionary<int, LayerRequestMapper> Mappers =
        new Dictionary<int, LayerRequestMapper>();
    private static readonly Dictionary<LayerEdge, LayerRequestEdge> Edges =
        new Dictionary<LayerEdge, LayerRequestEdge>();

    public static IReadOnlyList<LayerEdge> Route { get; } = CreateRoute();

    public static bool TryCreate(int layer, out LayerRequestMapper mapper)
    {
        int start = IndexOfSource(layer);
        if (start < 0)
        {
            mapper = null!;
            return false;
        }

        lock (Gate)
        {
            if (Mappers.TryGetValue(layer, out LayerRequestMapper? cached))
            {
                mapper = cached;
                return true;
            }

            var edges = new List<LayerRequestEdge>();
            for (int index = start; index < Route.Count; index++)
            {
                edges.Add(GetEdge(Route[index]));
            }
            mapper = new LayerRequestMapper(edges, LayerSchemaCatalog.CompatMethods);
            Mappers.Add(layer, mapper);
            return true;
        }
    }

    public static LayerRequestEdge GetEdge(LayerEdge edge)
    {
        lock (Gate)
        {
            if (Edges.TryGetValue(edge, out LayerRequestEdge? cached))
            {
                return cached;
            }
            if (!Route.Contains(edge))
            {
                throw new ArgumentException("The request route has no edge " + edge + ".",
                    nameof(edge));
            }

            var value = new LayerRequestEdge(LayerSchemaCatalog.Create(edge.Source),
                LayerSchemaCatalog.Create(edge.Target),
                PublishedRequestConverters.Defaults.Where(x => x.Edge.Equals(edge)),
                PublishedRequestConverters.Converters.Where(x => x.Edge.Equals(edge)));
            Edges.Add(edge, value);
            return value;
        }
    }

    private static IReadOnlyList<LayerEdge> CreateRoute()
    {
        IReadOnlyList<int> layers = LayerSchemaCatalog.Layers;
        var route = new List<LayerEdge>();
        for (int index = 1; index < layers.Count &&
                            layers[index - 1] < LayerSchemaCatalog.BaseLayer; index++)
        {
            route.Add(new LayerEdge(layers[index - 1], layers[index]));
        }
        RequireDeclarationsOnRoute(route);
        return route.AsReadOnly();
    }

    private static void RequireDeclarationsOnRoute(List<LayerEdge> route)
    {
        IEnumerable<LayerEdge> declared = PublishedRequestConverters.Defaults
            .Select(x => x.Edge)
            .Concat(PublishedRequestConverters.Converters.Select(x => x.Edge));
        foreach (LayerEdge edge in declared)
        {
            if (!route.Contains(edge))
            {
                throw new InvalidOperationException(
                    "A request declaration names an edge off the request route: " +
                    edge + ".");
            }
        }
    }

    private static int IndexOfSource(int layer)
    {
        for (int index = 0; index < Route.Count; index++)
        {
            if (Route[index].Source == layer)
            {
                return index;
            }
        }
        return -1;
    }
}
