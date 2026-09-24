// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Globalization;
using System.IO;
using System.Reflection;

namespace Ferrite.TL.Schema;

public static class LayerTransformEdgeCatalog
{
    private const string Resource =
        "Ferrite.TL.Schema.Schemas.dispositions.tsv";

    public static IReadOnlyList<ILayerTransformEdge> Create(
        IReadOnlyDictionary<string, LayerSemanticConverter>? converters = null)
    {
        var order = new List<KeyValuePair<int, int>>();
        var groups = new Dictionary<KeyValuePair<int, int>,
            List<LayerConstructorDisposition>>();
        foreach (string[] row in Rows())
        {
            var edge = new KeyValuePair<int, int>(
                int.Parse(row[0], CultureInfo.InvariantCulture),
                int.Parse(row[1], CultureInfo.InvariantCulture));
            if (!groups.TryGetValue(edge,
                    out List<LayerConstructorDisposition>? dispositions))
            {
                dispositions = new List<LayerConstructorDisposition>();
                groups.Add(edge, dispositions);
                order.Add(edge);
            }
            LayerDispositionKind kind = Kind(row[3]);
            string? argument = row[4].Length == 0 ? null : row[4];
            dispositions.Add(kind == LayerDispositionKind.Unwrap
                ? new LayerConstructorDisposition(row[2], kind, field: argument)
                : new LayerConstructorDisposition(row[2], kind,
                    converterName: argument));
        }

        var edges = new ILayerTransformEdge[order.Count];
        for (int index = 0; index < order.Count; index++)
        {
            KeyValuePair<int, int> edge = order[index];
            edges[index] = new LayerTransformEdgeDefinition(
                LayerSchemaCatalog.Create(edge.Key),
                LayerSchemaCatalog.Create(edge.Value),
                groups[edge].ToArray(), converters);
        }
        return edges;
    }

    private static IEnumerable<string[]> Rows()
    {
        Stream stream = typeof(LayerTransformEdgeCatalog).GetTypeInfo()
                            .Assembly.GetManifestResourceStream(Resource) ??
                        throw new InvalidOperationException(
                            "Missing schema resource: " + Resource);
        using (stream)
        using (var reader = new StreamReader(stream))
        {
            reader.ReadLine();
            string? line = reader.ReadLine();
            while (line != null)
            {
                if (line.Length > 0)
                {
                    yield return line.Split('\t');
                }
                line = reader.ReadLine();
            }
        }
    }

    private static LayerDispositionKind Kind(string value)
    {
        switch (value)
        {
            case "Mechanical":
                return LayerDispositionKind.Mechanical;
            case "Semantic":
                return LayerDispositionKind.Semantic;
            case "ProvenNotEmitted":
                return LayerDispositionKind.ProvenNotEmitted;
            case "Unsupported":
                return LayerDispositionKind.Unsupported;
            case "Stream":
                return LayerDispositionKind.Stream;
            case "NoTargetConstructor":
                return LayerDispositionKind.NoTargetConstructor;
            case "Unwrap":
                return LayerDispositionKind.Unwrap;
            case "Omit":
                return LayerDispositionKind.Omit;
            default:
                throw new InvalidOperationException(
                    "Unknown layer disposition: " + value);
        }
    }
}
