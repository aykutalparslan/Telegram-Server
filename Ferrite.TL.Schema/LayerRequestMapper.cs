// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.TL.Schema;

public sealed class LayerRequestMapper
{
    private readonly IReadOnlyList<LayerRequestEdge> _edges;
    private readonly IReadOnlyDictionary<int, LayerConstructor> _compatMethods;

    public LayerEdge Edge { get; }
    public LayerSchema SourceSchema { get; }
    public LayerSchema TargetSchema { get; }
    public IReadOnlyList<LayerRequestEdge> Edges => _edges;

    public LayerRequestMapper(IEnumerable<LayerRequestEdge> edges,
        IReadOnlyDictionary<int, LayerConstructor>? compatMethods = null)
    {
        LayerRequestEdge[] values = edges.ToArray();
        if (values.Length == 0)
        {
            throw new ArgumentException("A request mapper needs at least one edge.",
                nameof(edges));
        }
        for (int index = 1; index < values.Length; index++)
        {
            if (values[index].Edge.Source != values[index - 1].Edge.Target)
            {
                throw new ArgumentException("Request edges are not contiguous.",
                    nameof(edges));
            }
        }

        _edges = Array.AsReadOnly(values);
        _compatMethods = compatMethods ?? new Dictionary<int, LayerConstructor>();
        SourceSchema = values[0].SourceSchema;
        TargetSchema = values[values.Length - 1].TargetSchema;
        Edge = new LayerEdge(SourceSchema.Layer, TargetSchema.Layer);
    }

    public LayerTransformResult Map(byte[] source)
    {
        int? constructor = source.Length >= 4
            ? source[0] | source[1] << 8 | source[2] << 16 | source[3] << 24
            : (int?)null;
        LayerEdge failing = Edge;
        try
        {
            var reader = new LayerWireReader(source);
            int id = reader.ReadInt32();
            if (!SourceSchema.TryGetMethod(id, out LayerConstructor sourceMethod) &&
                !_compatMethods.TryGetValue(id, out sourceMethod))
            {
                throw new LayerWireException("Unknown source method 0x" +
                                             unchecked((uint)id).ToString("x8") + ".");
            }

            LayerObjectValue value = LayerWireCodec.ReadObjectBody(
                reader, SourceSchema, sourceMethod);
            if (!reader.IsComplete)
            {
                throw new LayerWireException("Trailing bytes remain after the request.");
            }

            foreach (LayerRequestEdge edge in _edges)
            {
                failing = edge.Edge;
                value = edge.MapMethod(value);
            }
            failing = Edge;

            var writer = new LayerWireWriter();
            writer.WriteInt32(value.Constructor.Id);
            LayerWireCodec.WriteObjectBody(writer, TargetSchema, value.Constructor, value);
            byte[] mapped = writer.ToArray();
            return source.SequenceEqual(mapped)
                ? LayerTransformResult.Identity(source)
                : LayerTransformResult.Transformed(mapped);
        }
        catch (LayerUncoveredException exception)
        {
            return LayerTransformResult.Failure(LayerTransformStatus.Uncovered,
                failing, constructor, exception.Message);
        }
        catch (Exception exception)
        {
            return LayerTransformResult.Failure(LayerTransformStatus.Malformed,
                failing, constructor, exception.Message);
        }
    }
}
