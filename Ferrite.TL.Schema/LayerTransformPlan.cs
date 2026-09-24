// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.TL.Schema;

public sealed class LayerTransformPlan
{
    private readonly IReadOnlyList<ILayerTransformEdge> _edges;

    public int SourceLayer { get; }
    public int TargetLayer { get; }
    public IReadOnlyList<ILayerTransformEdge> Edges => _edges;

    public LayerTransformPlan(int sourceLayer, int targetLayer,
        IEnumerable<ILayerTransformEdge> edges)
    {
        SourceLayer = sourceLayer;
        TargetLayer = targetLayer;
        ILayerTransformEdge[] values = edges.ToArray();
        int current = sourceLayer;
        foreach (ILayerTransformEdge edge in values)
        {
            if (edge.Edge.Source != current)
            {
                throw new ArgumentException("Transform plan edges are not contiguous.",
                    nameof(edges));
            }
            current = edge.Edge.Target;
        }
        if (current != targetLayer)
        {
            throw new ArgumentException("Transform plan does not reach its target.",
                nameof(edges));
        }
        _edges = Array.AsReadOnly(values);
    }

    public bool TryGetVectorResultElement(int methodConstructor,
        out string elementType)
    {
        if (_edges.Count == 0)
        {
            elementType = null!;
            return false;
        }
        LayerSchema baseSchema = _edges[0].SourceSchema;
        if (baseSchema.TryGetVectorResultElement(methodConstructor, out elementType) ||
            LayerSchemaCatalog.CompatVectorResults.TryGetValue(methodConstructor,
                out elementType!))
        {
            return true;
        }
        if (_edges[_edges.Count - 1].TargetSchema.TryGetMethod(methodConstructor,
                out LayerConstructor method) &&
            baseSchema.TryGetMethod(method.Name, out LayerConstructor baseMethod))
        {
            return baseSchema.TryGetVectorResultElement(baseMethod.Id, out elementType);
        }
        elementType = null!;
        return false;
    }

    public LayerTransformResult Transform(byte[] source)
    {
        return Transform(source, null);
    }

    public LayerTransformResult Transform(byte[] source, string? vectorElementType)
    {
        byte[] current = source;
        bool changed = false;
        foreach (ILayerTransformEdge edge in _edges)
        {
            LayerTransformResult result = edge.Transform(current, vectorElementType);
            if (!result.IsSuccess)
            {
                return result.AtEdge(edge.Edge);
            }
            if (result.Status == LayerTransformStatus.Transformed)
            {
                changed = true;
            }
            current = result.Data!;
            vectorElementType = result.VectorElementType ?? vectorElementType;
        }
        return changed
            ? LayerTransformResult.Transformed(current)
            : LayerTransformResult.Identity(source);
    }
}

public sealed class LayerTransformRegistry : ILayerTransformRegistry
{
    private readonly IReadOnlyList<int> _route;
    private readonly IReadOnlyDictionary<LayerEdge, ILayerTransformEdge> _edges;

    public int BaseLayer { get; }
    public IReadOnlyList<int> Route => _route;

    public LayerTransformRegistry(IEnumerable<int> route,
        IEnumerable<ILayerTransformEdge> edges)
    {
        int[] routeValues = route.ToArray();
        if (routeValues.Length == 0 || routeValues.Distinct().Count() != routeValues.Length)
        {
            throw new ArgumentException("Transform route must contain unique layers.",
                nameof(route));
        }
        BaseLayer = routeValues[0];
        _route = Array.AsReadOnly(routeValues);
        var edgeValues = new Dictionary<LayerEdge, ILayerTransformEdge>();
        foreach (ILayerTransformEdge edge in edges)
        {
            if (edgeValues.ContainsKey(edge.Edge))
            {
                throw new ArgumentException("Duplicate transform edge.", nameof(edges));
            }
            edgeValues.Add(edge.Edge, edge);
        }
        _edges = new System.Collections.ObjectModel.ReadOnlyDictionary<LayerEdge, ILayerTransformEdge>(
            edgeValues);
    }

    public static LayerTransformRegistry Identity(int baseLayer)
    {
        return new LayerTransformRegistry(new[] { baseLayer },
            Array.Empty<ILayerTransformEdge>());
    }

    public bool TryCreatePlan(int targetLayer, out LayerTransformPlan plan,
        out string? error)
    {
        int targetIndex = -1;
        for (int i = 0; i < _route.Count; i++)
        {
            if (_route[i] == targetLayer)
            {
                targetIndex = i;
                break;
            }
        }
        if (targetIndex < 0)
        {
            plan = null!;
            error = "Target layer is not in the transform route.";
            return false;
        }

        var selected = new List<ILayerTransformEdge>();
        for (int i = 0; i < targetIndex; i++)
        {
            var key = new LayerEdge(_route[i], _route[i + 1]);
            if (!_edges.TryGetValue(key, out ILayerTransformEdge? edge))
            {
                plan = null!;
                error = "Transform edge is missing: " + key + ".";
                return false;
            }
            if (edge.HasUnsupportedDispositions)
            {
                plan = null!;
                error = "Transform edge contains an Unsupported disposition: " + key + ".";
                return false;
            }
            selected.Add(edge);
        }

        plan = new LayerTransformPlan(BaseLayer, targetLayer, selected);
        error = null;
        return true;
    }
}

public static class PublishedLayerTransformRoute
{
    public static IReadOnlyList<int> All { get; } =
        Array.AsReadOnly(LayerSchemaCatalog.Layers.Reverse().ToArray());
}
