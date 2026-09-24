// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Globalization;

namespace Ferrite.TL.Schema;

public sealed class LayerSchemaSymbol
{
    private readonly IReadOnlyList<LayerField> _fields;

    public bool IsMethod { get; }
    public string Name { get; }
    public int Id { get; }
    public string ResultType { get; }
    public string? VectorElementType { get; }
    public IReadOnlyList<LayerField> Fields => _fields;

    public LayerSchemaSymbol(bool isMethod, string name, int id, string resultType,
        string? vectorElementType, IEnumerable<LayerField> fields)
    {
        IsMethod = isMethod;
        Name = name;
        Id = id;
        ResultType = resultType;
        VectorElementType = vectorElementType;
        _fields = Array.AsReadOnly(fields.ToArray());
    }

    public LayerConstructor ToConstructor()
    {
        return new LayerConstructor(Name, Id, ResultType, _fields);
    }
}

public static class LayerSchemaReader
{
    internal const int VectorConstructorId = unchecked((int)0x1cb5c415);
    private const string VectorIdentifier = "vector";
    private const string VectorResultType = "Vector";

    public static IReadOnlyList<LayerSchemaSymbol> Read(string source)
    {
        if (!source.EndsWith("\n", StringComparison.Ordinal))
        {
            source += "\n";
        }

        var parser = new Parser(new Lexer(source));
        var symbols = new List<LayerSchemaSymbol>();
        CombinatorDeclarationSyntax? combinator = parser.ParseCombinator();
        while (combinator != null)
        {
            LayerSchemaSymbol? symbol = Convert(combinator);
            if (symbol != null)
            {
                symbols.Add(symbol);
            }
            combinator = parser.ParseCombinator();
        }

        return symbols;
    }

    public static LayerSchema Build(int layer, IEnumerable<LayerSchemaSymbol> symbols)
    {
        var constructors = new List<LayerConstructor>();
        var methods = new List<LayerConstructor>();
        var vectorResults = new List<KeyValuePair<int, string>>();
        foreach (LayerSchemaSymbol symbol in symbols)
        {
            if (symbol.IsMethod)
            {
                if (symbol.VectorElementType != null)
                {
                    vectorResults.Add(new KeyValuePair<int, string>(
                        symbol.Id, symbol.VectorElementType));
                }
                methods.Add(symbol.ToConstructor());
                continue;
            }

            constructors.Add(symbol.ToConstructor());
        }

        return new LayerSchema(layer, constructors, vectorResults, methods);
    }

    public static LayerSchema Read(int layer, string source)
    {
        return Build(layer, Read(source));
    }

    public static LayerType ConvertType(TypeTermSyntax type)
    {
        string identifier = type.Identifier ??
                            throw new InvalidOperationException("Schema type is incomplete.");
        switch (identifier)
        {
            case "#":
                return new LayerType(LayerWireKind.Flags);
            case "int":
                return new LayerType(LayerWireKind.Int32);
            case "long":
                return new LayerType(LayerWireKind.Int64);
            case "double":
                return new LayerType(LayerWireKind.Double);
            case "int128":
                return new LayerType(LayerWireKind.Int128);
            case "int256":
                return new LayerType(LayerWireKind.Int256);
            case "int512":
                return new LayerType(LayerWireKind.Int512);
            case "bytes":
                return new LayerType(LayerWireKind.Bytes);
            case "string":
                return new LayerType(LayerWireKind.String);
            case "true":
                return new LayerType(LayerWireKind.True);
            case "Vector":
            case "vector":
                if (type.OptionalType == null)
                {
                    throw new InvalidOperationException("Vector element type is missing.");
                }
                return new LayerType(LayerWireKind.Vector, null,
                    identifier == "vector" || type.IsBare,
                    ConvertType(type.OptionalType));
            default:
                return new LayerType(LayerWireKind.Object,
                    type.IsTypeOf ? null : Qualify(type.NamespaceIdentifier, identifier),
                    type.IsBare);
        }
    }

    public static string Qualify(string? nameSpace, string identifier)
    {
        return nameSpace == null ? identifier : nameSpace + "." + identifier;
    }

    private static LayerSchemaSymbol? Convert(CombinatorDeclarationSyntax combinator)
    {
        if (combinator.CombinatorType == CombinatorType.Builtin)
        {
            return ConvertVector(combinator);
        }

        if (combinator.Name == null || combinator.Identifier == null ||
            combinator.Type?.Identifier == null)
        {
            return null;
        }

        bool isMethod = combinator.CombinatorType == CombinatorType.Function;
        var fields = new List<LayerField>();
        if (combinator.Arguments != null)
        {
            foreach (SimpleArgumentSyntax argument in combinator.Arguments)
            {
                if (argument.Identifier == null || argument.TypeTerm == null)
                {
                    throw new InvalidOperationException("Schema argument is incomplete.");
                }
                fields.Add(new LayerField(argument.Identifier,
                    ConvertType(argument.TypeTerm),
                    argument.ConditionalDefinition?.Identifier,
                    argument.ConditionalDefinition?.ConditionalArgumentBit));
            }
        }

        uint id = uint.Parse(combinator.Name, NumberStyles.AllowHexSpecifier,
            CultureInfo.InvariantCulture);
        return new LayerSchemaSymbol(isMethod,
            Qualify(combinator.Namespace, combinator.Identifier),
            unchecked((int)id),
            Qualify(combinator.Type.NamespaceIdentifier, combinator.Type.Identifier),
            VectorElementType(combinator.Type),
            fields);
    }

    private static LayerSchemaSymbol? ConvertVector(CombinatorDeclarationSyntax combinator)
    {
        if (combinator.Identifier != VectorIdentifier ||
            combinator.Type?.Identifier != VectorResultType ||
            combinator.OptionalArguments == null ||
            combinator.OptionalArguments.Count != 1 ||
            combinator.OptionalArguments[0].Identifier == null)
        {
            return null;
        }

        return new LayerSchemaSymbol(false, VectorIdentifier,
            VectorConstructorId,
            VectorResultType + " " + combinator.OptionalArguments[0].Identifier,
            null, Array.Empty<LayerField>());
    }

    private static string? VectorElementType(TypeTermSyntax type)
    {
        if (type.Identifier != "Vector" || type.OptionalType?.Identifier == null)
        {
            return null;
        }
        return Qualify(type.OptionalType.NamespaceIdentifier, type.OptionalType.Identifier);
    }
}
