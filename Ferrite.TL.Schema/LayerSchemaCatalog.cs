// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace Ferrite.TL.Schema;

public static class LayerSchemaCatalog
{
    public const int BaseLayer = 229;

    private const string Prefix = "Ferrite.TL.Schema.Schemas.";
    private const string CompatPrefix = "Ferrite.TL.Schema.Compat.";
    private const string BaseResource = Prefix + "baseLayer.tl";
    private const string IntroductionsResource = Prefix + "introductions.tsv";
    private const string DtoNamespace = "dto.";
    private const string PrefixMethodSuffix = "Prefix";

    private static readonly object Gate = new object();
    private static readonly Dictionary<int, LayerSchema> Cache = new Dictionary<int, LayerSchema>();
    private static IReadOnlyList<LayerSchemaSymbol>? _baseSymbols;
    private static IReadOnlyDictionary<string, int>? _introductions;
    private static IReadOnlyDictionary<int, string>? _compatVectorResults;
    private static IReadOnlyDictionary<int, LayerConstructor>? _compatMethods;

    public static IReadOnlyList<int> Layers { get; } = DiscoverLayers();

    public static IReadOnlyDictionary<int, string> CompatVectorResults
    {
        get
        {
            lock (Gate)
            {
                if (_compatVectorResults == null)
                {
                    _compatVectorResults = BuildCompatVectorResults();
                }
                return _compatVectorResults;
            }
        }
    }

    public static IReadOnlyDictionary<int, LayerConstructor> CompatMethods
    {
        get
        {
            lock (Gate)
            {
                if (_compatMethods == null)
                {
                    _compatMethods = BuildCompatMethods();
                }
                return _compatMethods;
            }
        }
    }

    public static LayerSchema Create(int layer)
    {
        lock (Gate)
        {
            if (Cache.TryGetValue(layer, out LayerSchema? cached))
            {
                return cached;
            }

            LayerSchema schema = LayerSchemaReader.Build(layer, Reconstruct(layer));
            Cache.Add(layer, schema);
            return schema;
        }
    }

    private static IReadOnlyDictionary<int, string> BuildCompatVectorResults()
    {
        IReadOnlyDictionary<int, string> published = Create(BaseLayer).VectorResults;
        var values = new Dictionary<int, string>();
        foreach (LayerSchemaSymbol symbol in CompatSymbols())
        {
            if (!symbol.IsMethod || symbol.VectorElementType == null ||
                published.ContainsKey(symbol.Id))
            {
                continue;
            }
            if (values.TryGetValue(symbol.Id, out string? existing) &&
                !string.Equals(existing, symbol.VectorElementType, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Conflicting compatibility vector result: " + symbol.Name + ".");
            }
            values[symbol.Id] = symbol.VectorElementType;
        }
        return new ReadOnlyDictionary<int, string>(values);
    }

    private static IReadOnlyDictionary<int, LayerConstructor> BuildCompatMethods()
    {
        var values = new Dictionary<int, LayerConstructor>();
        foreach (LayerSchemaSymbol symbol in CompatSymbols())
        {
            if (!symbol.IsMethod)
            {
                continue;
            }
            LayerConstructor method = symbol.ToConstructor();
            if (values.TryGetValue(symbol.Id, out LayerConstructor? existing) &&
                !existing.HasSameShape(method))
            {
                throw new InvalidOperationException(
                    "Conflicting compatibility method: " + symbol.Name + ".");
            }
            values[symbol.Id] = method;
        }
        return new ReadOnlyDictionary<int, LayerConstructor>(values);
    }

    private static IEnumerable<LayerSchemaSymbol> CompatSymbols()
    {
        foreach (string name in typeof(LayerSchemaCatalog).GetTypeInfo()
                     .Assembly.GetManifestResourceNames())
        {
            if (!name.StartsWith(CompatPrefix, StringComparison.Ordinal) ||
                !name.EndsWith(".tl", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (LayerSchemaSymbol symbol in LayerSchemaReader.Read(ReadResource(name)))
            {
                yield return symbol;
            }
        }
    }

    private static IEnumerable<LayerSchemaSymbol> Reconstruct(int layer)
    {
        IReadOnlyList<LayerSchemaSymbol> baseSymbols = BaseSymbols();
        if (layer == BaseLayer)
        {
            return Order(baseSymbols);
        }

        if (!Layers.Contains(layer))
        {
            throw new ArgumentOutOfRangeException(nameof(layer), layer,
                "No schema is published for this layer.");
        }

        IReadOnlyDictionary<string, int> introductions = Introductions();
        var symbols = new Dictionary<string, LayerSchemaSymbol>(StringComparer.Ordinal);
        foreach (LayerSchemaSymbol symbol in baseSymbols)
        {
            string key = Key(symbol);
            if (introductions.TryGetValue(key, out int introduced) && introduced > layer)
            {
                continue;
            }
            symbols[key] = symbol;
        }

        foreach (LayerSchemaSymbol symbol in LayerSchemaReader.Read(
                     ReadResource(Prefix + "layer" + layer.ToString(
                         CultureInfo.InvariantCulture) + ".tl")))
        {
            symbols[Key(symbol)] = symbol;
        }

        return Order(symbols.Values);
    }

    private static IReadOnlyList<LayerSchemaSymbol> BaseSymbols()
    {
        if (_baseSymbols == null)
        {
            _baseSymbols = LayerSchemaReader.Read(ReadResource(BaseResource))
                .Where(IsPublished)
                .ToArray();
        }
        return _baseSymbols;
    }

    private static bool IsPublished(LayerSchemaSymbol symbol)
    {
        if (symbol.Name.StartsWith(DtoNamespace, StringComparison.Ordinal))
        {
            return false;
        }
        return !symbol.IsMethod ||
               !symbol.Name.EndsWith(PrefixMethodSuffix, StringComparison.Ordinal);
    }

    private static IReadOnlyDictionary<string, int> Introductions()
    {
        if (_introductions == null)
        {
            var values = new Dictionary<string, int>(StringComparer.Ordinal);
            using (var reader = new StringReader(ReadResource(IntroductionsResource)))
            {
                reader.ReadLine();
                string? line = reader.ReadLine();
                while (line != null)
                {
                    if (line.Length > 0)
                    {
                        string[] parts = line.Split('\t');
                        values[Key(parts[0] == "method", parts[1])] =
                            int.Parse(parts[2], CultureInfo.InvariantCulture);
                    }
                    line = reader.ReadLine();
                }
            }
            _introductions = values;
        }
        return _introductions;
    }

    private static IEnumerable<LayerSchemaSymbol> Order(
        IEnumerable<LayerSchemaSymbol> symbols)
    {
        return symbols
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ThenBy(x => x.IsMethod);
    }

    private static string Key(LayerSchemaSymbol symbol)
    {
        return Key(symbol.IsMethod, symbol.Name);
    }

    private static string Key(bool isMethod, string name)
    {
        return isMethod ? "m:" + name : "c:" + name;
    }

    private static IReadOnlyList<int> DiscoverLayers()
    {
        var layers = new List<int> { BaseLayer };
        foreach (string name in typeof(LayerSchemaCatalog).GetTypeInfo()
                     .Assembly.GetManifestResourceNames())
        {
            if (!name.StartsWith(Prefix, StringComparison.Ordinal) ||
                !name.EndsWith(".tl", StringComparison.Ordinal))
            {
                continue;
            }
            string stem = name.Substring(Prefix.Length, name.Length - Prefix.Length - 3);
            if (stem.StartsWith("layer", StringComparison.Ordinal) &&
                int.TryParse(stem.Substring(5), NumberStyles.None,
                    CultureInfo.InvariantCulture, out int layer) &&
                !layers.Contains(layer))
            {
                layers.Add(layer);
            }
        }
        layers.Sort();
        return layers.AsReadOnly();
    }

    private static string ReadResource(string name)
    {
        Stream stream = typeof(LayerSchemaCatalog).GetTypeInfo()
                            .Assembly.GetManifestResourceStream(name) ??
                        throw new InvalidOperationException(
                            "Missing schema resource: " + name);
        using (stream)
        using (var reader = new StreamReader(stream))
        {
            return reader.ReadToEnd();
        }
    }
}
