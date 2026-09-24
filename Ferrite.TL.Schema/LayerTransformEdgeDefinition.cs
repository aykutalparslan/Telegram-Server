// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Collections.ObjectModel;

namespace Ferrite.TL.Schema;

public sealed class LayerTransformEdgeDefinition : ILayerTransformEdge
{
    private readonly IReadOnlyDictionary<string, LayerConstructorDisposition> _dispositions;
    private readonly IReadOnlyDictionary<string, LayerSemanticConverter> _converters;
    private readonly IReadOnlyList<LayerConstructorDisposition> _dispositionValues;
    private readonly IReadOnlyDictionary<string, LayerType> _scalarUnwraps;

    public LayerEdge Edge { get; }
    public LayerSchema SourceSchema { get; }
    public LayerSchema TargetSchema { get; }
    public bool HasUnsupportedDispositions { get; }
    public IReadOnlyList<LayerConstructorDisposition> Dispositions => _dispositionValues;

    public LayerTransformEdgeDefinition(LayerSchema sourceSchema,
        LayerSchema targetSchema,
        IEnumerable<LayerConstructorDisposition> dispositions,
        IReadOnlyDictionary<string, LayerSemanticConverter>? converters = null)
    {
        if (sourceSchema.Layer == targetSchema.Layer)
        {
            throw new ArgumentException("An edge must join different layers.");
        }

        SourceSchema = sourceSchema;
        TargetSchema = targetSchema;
        Edge = new LayerEdge(sourceSchema.Layer, targetSchema.Layer);

        LayerConstructorDisposition[] values = dispositions.ToArray();
        var byConstructor = new Dictionary<string, LayerConstructorDisposition>(StringComparer.Ordinal);
        foreach (LayerConstructorDisposition disposition in values)
        {
            if (byConstructor.ContainsKey(disposition.SourceConstructor))
            {
                throw new ArgumentException("Duplicate constructor disposition.",
                    nameof(dispositions));
            }
            byConstructor.Add(disposition.SourceConstructor, disposition);
        }

        _dispositions = new ReadOnlyDictionary<string, LayerConstructorDisposition>(byConstructor);
        _dispositionValues = Array.AsReadOnly(values);
        var converterValues = new Dictionary<string, LayerSemanticConverter>(
            StringComparer.Ordinal);
        if (converters != null)
        {
            foreach (KeyValuePair<string, LayerSemanticConverter> converter in converters)
            {
                converterValues.Add(converter.Key, converter.Value);
            }
        }
        _converters = new ReadOnlyDictionary<string, LayerSemanticConverter>(
            converterValues);

        ValidateCoverage();
        _scalarUnwraps = ScalarUnwraps();
        HasUnsupportedDispositions = values.Any(x =>
            x.Kind == LayerDispositionKind.Unsupported);
    }

    public LayerTransformResult Transform(byte[] source)
    {
        return Transform(source, null);
    }

    public LayerTransformResult Transform(byte[] source, string? vectorElementType)
    {
        int? constructor = source.Length >= 4
            ? source[0] | source[1] << 8 | source[2] << 16 | source[3] << 24
            : (int?)null;
        try
        {
            var reader = new LayerWireReader(source);
            var writer = new LayerWireWriter();
            if (vectorElementType != null)
            {
                LayerType rootType = VectorResultType(vectorElementType);
                object rootValue = LayerWireCodec.ReadValue(reader, SourceSchema, rootType);
                if (!reader.IsComplete)
                {
                    throw new LayerWireException(
                        "Trailing bytes remain after the root vector.");
                }
                if (_scalarUnwraps.TryGetValue(vectorElementType, out LayerType? scalarType))
                {
                    LayerWireCodec.WriteValue(writer, TargetSchema,
                        new LayerType(LayerWireKind.Vector, null, false, scalarType),
                        UnwrapScalars((IReadOnlyList<object?>)rootValue));
                    return LayerTransformResult.TransformedVector(writer.ToArray(),
                        VectorElementName(scalarType));
                }
                LayerWireCodec.WriteValue(writer, TargetSchema, rootType,
                    TransformValue(rootType, rootType, rootValue));
                byte[] rootBytes = writer.ToArray();
                return source.SequenceEqual(rootBytes)
                    ? LayerTransformResult.Identity(source)
                    : LayerTransformResult.Transformed(rootBytes);
            }

            LayerObjectValue sourceValue = LayerWireCodec.ReadBoxedObject(reader, SourceSchema);
            if (!reader.IsComplete)
            {
                throw new LayerWireException("Trailing bytes remain after the root object.");
            }

            if (TryUnwrapVector(sourceValue, out LayerType vectorType, out object? vector))
            {
                LayerWireCodec.WriteValue(writer, TargetSchema, vectorType,
                    TransformValue(vectorType, vectorType, vector));
                return LayerTransformResult.TransformedVector(writer.ToArray(),
                    VectorElementName(vectorType.ElementType!));
            }

            LayerObjectValue targetValue = TransformObject(sourceValue);
            LayerWireCodec.WriteBoxedObject(writer, TargetSchema, targetValue);
            byte[] transformed = writer.ToArray();
            if (source.SequenceEqual(transformed))
            {
                return LayerTransformResult.Identity(source);
            }
            return LayerTransformResult.Transformed(transformed);
        }
        catch (LayerUncoveredException exception)
        {
            return LayerTransformResult.Failure(LayerTransformStatus.Uncovered,
                Edge, constructor, exception.Message);
        }
        catch (LayerWireException exception)
        {
            return LayerTransformResult.Failure(LayerTransformStatus.Malformed,
                Edge, constructor, exception.Message);
        }
        catch (Exception exception)
        {
            return LayerTransformResult.Failure(LayerTransformStatus.Malformed,
                Edge, constructor, exception.Message);
        }
    }

    private static LayerType VectorResultType(string elementType)
    {
        return new LayerType(LayerWireKind.Vector, null, false,
            VectorElementType(elementType));
    }

    private static LayerType VectorElementType(string elementType)
    {
        switch (elementType)
        {
            case "int":
                return new LayerType(LayerWireKind.Int32);
            case "long":
                return new LayerType(LayerWireKind.Int64);
            case "double":
                return new LayerType(LayerWireKind.Double);
            case "string":
                return new LayerType(LayerWireKind.String);
            case "bytes":
                return new LayerType(LayerWireKind.Bytes);
            default:
                return new LayerType(LayerWireKind.Object, elementType);
        }
    }

    private static string VectorElementName(LayerType elementType)
    {
        switch (elementType.Kind)
        {
            case LayerWireKind.Int32:
                return "int";
            case LayerWireKind.Int64:
                return "long";
            case LayerWireKind.Double:
                return "double";
            case LayerWireKind.String:
                return "string";
            case LayerWireKind.Bytes:
                return "bytes";
            default:
                return elementType.Name ??
                       throw new LayerUncoveredException("A vector element type has no name.");
        }
    }

    private bool TryUnwrapVector(LayerObjectValue source, out LayerType vectorType,
        out object? vector)
    {
        vectorType = null!;
        vector = null;
        if (!_dispositions.TryGetValue(source.Constructor.Name,
                out LayerConstructorDisposition? disposition) ||
            disposition.Kind != LayerDispositionKind.Unwrap)
        {
            return false;
        }
        LayerField field = UnwrappedField(source, disposition, out object? value);
        if (field.Type.Kind != LayerWireKind.Vector)
        {
            return false;
        }
        vectorType = field.Type;
        vector = value;
        return true;
    }

    private IReadOnlyList<object?> UnwrapScalars(IReadOnlyList<object?> values)
    {
        var scalars = new object?[values.Count];
        for (int index = 0; index < values.Count; index++)
        {
            var value = (LayerObjectValue)values[index]!;
            UnwrappedField(value, _dispositions[value.Constructor.Name], out scalars[index]);
        }
        return Array.AsReadOnly(scalars);
    }

    private IReadOnlyDictionary<string, LayerType> ScalarUnwraps()
    {
        var scalars = new Dictionary<string, LayerType>(StringComparer.Ordinal);
        foreach (LayerConstructorDisposition disposition in _dispositionValues)
        {
            LayerConstructor source = SourceSchema.GetConstructor(disposition.SourceConstructor);
            LayerType? type = disposition.Kind == LayerDispositionKind.Unwrap
                ? source.FindField(disposition.Field!)!.Type
                : null;
            if (type == null || !IsScalar(type))
            {
                continue;
            }
            foreach (LayerConstructor sibling in SourceSchema.Constructors)
            {
                if (sibling.ResultType != source.ResultType)
                {
                    continue;
                }
                if (!_dispositions.TryGetValue(sibling.Name,
                        out LayerConstructorDisposition? unwrap) ||
                    unwrap.Kind != LayerDispositionKind.Unwrap ||
                    !sibling.FindField(unwrap.Field!)!.Type.HasSameWireType(type))
                {
                    throw new ArgumentException("Every constructor of " + source.ResultType +
                                                " must unwrap the same scalar.");
                }
            }
            scalars[source.ResultType] = type;
        }
        return new ReadOnlyDictionary<string, LayerType>(scalars);
    }

    private static bool IsScalar(LayerType type)
    {
        return type.Kind is LayerWireKind.Int32 or LayerWireKind.Int64
            or LayerWireKind.Double or LayerWireKind.String or LayerWireKind.Bytes;
    }

    private static LayerField UnwrappedField(LayerObjectValue source,
        LayerConstructorDisposition disposition, out object? value)
    {
        LayerField field = source.Constructor.FindField(disposition.Field!)!;
        if (!source.TryGetValue(field.Name, out value) || value == null)
        {
            throw new LayerUncoveredException("Unwrapped field is absent: " +
                                              source.Constructor.Name + "." + field.Name + ".");
        }
        return field;
    }

    private void ValidateCoverage()
    {
        var changed = new HashSet<string>(StringComparer.Ordinal);
        foreach (LayerConstructor source in SourceSchema.Constructors)
        {
            if (!TargetSchema.TryGetConstructor(source.Name, out LayerConstructor target) ||
                !source.HasSameShape(target))
            {
                changed.Add(source.Name);
            }
        }

        foreach (string name in changed)
        {
            if (!_dispositions.ContainsKey(name))
            {
                throw new ArgumentException("Missing constructor disposition for " + name + ".");
            }
        }

        foreach (LayerConstructorDisposition disposition in _dispositionValues)
        {
            if (!changed.Contains(disposition.SourceConstructor))
            {
                throw new ArgumentException("Disposition does not match a schema change for " +
                                            disposition.SourceConstructor + ".");
            }
            if (!SourceSchema.TryGetConstructor(disposition.SourceConstructor, out LayerConstructor source))
            {
                throw new ArgumentException("Disposition names an unknown source constructor.");
            }

            if (disposition.Kind == LayerDispositionKind.Mechanical)
            {
                string targetName = disposition.TargetConstructor ?? source.Name;
                if (!TargetSchema.TryGetConstructor(targetName, out LayerConstructor target))
                {
                    throw new ArgumentException("Mechanical target constructor is missing.");
                }
                ValidateMechanicalMap(source, target);
            }
            else if (disposition.Kind == LayerDispositionKind.Semantic)
            {
                if (string.IsNullOrWhiteSpace(disposition.ConverterName))
                {
                    throw new ArgumentException("A semantic disposition requires a converter name.");
                }
            }
            else if (disposition.Kind == LayerDispositionKind.Unwrap)
            {
                LayerField? field = disposition.Field == null
                    ? null
                    : source.FindField(disposition.Field);
                if (field == null ||
                    field.Type.Kind is not (LayerWireKind.Object or LayerWireKind.Vector) &&
                    !IsScalar(field.Type))
                {
                    throw new ArgumentException("An unwrap disposition must name an object, vector or scalar field of " +
                                                source.Name + ".");
                }
            }
        }
    }

    private static void ValidateMechanicalMap(LayerConstructor source,
        LayerConstructor target)
    {
        foreach (LayerField targetField in target.Fields)
        {
            if (targetField.Type.Kind == LayerWireKind.Flags)
            {
                continue;
            }

            LayerField? sourceField = source.FindField(targetField.Name);
            if (sourceField == null ||
                !sourceField.Type.HasSameWireType(targetField.Type) ||
                sourceField.IsConditional != targetField.IsConditional)
            {
                throw new ArgumentException("Mechanical field map is not wire-compatible for " +
                                            source.Name + "." + targetField.Name + ".");
            }
        }
    }

    private LayerObjectValue TransformObject(LayerObjectValue source)
    {
        if (_dispositions.TryGetValue(source.Constructor.Name,
                out LayerConstructorDisposition? disposition))
        {
            switch (disposition.Kind)
            {
                case LayerDispositionKind.Mechanical:
                    return TransformMechanical(source,
                        disposition.TargetConstructor ?? source.Constructor.Name);
                case LayerDispositionKind.Semantic:
                    if (!_converters.TryGetValue(disposition.ConverterName!,
                            out LayerSemanticConverter? converter))
                    {
                        throw new LayerUncoveredException("Missing semantic converter " +
                                                          disposition.ConverterName + ".");
                    }
                    LayerObjectValue? converted;
                    try
                    {
                        converted = converter(
                            new LayerConversionContext(SourceSchema, TargetSchema,
                                (value, target, fields, _) =>
                                    TransformCompatible(value, target, fields),
                                TransformObject), source);
                    }
                    catch (Exception exception)
                    {
                        throw new LayerUncoveredException("Semantic converter " +
                                                          disposition.ConverterName +
                                                          " failed: " + exception.Message);
                    }
                    if (converted == null)
                    {
                        throw new LayerUncoveredException("Semantic converter " +
                                                          disposition.ConverterName +
                                                          " did not produce a value.");
                    }
                    if (!TargetSchema.TryGetConstructor(converted.Constructor.Name, out _))
                    {
                        throw new LayerUncoveredException("Semantic converter " +
                                                          disposition.ConverterName +
                                                          " produced an unknown target constructor.");
                    }
                    return converted;
                case LayerDispositionKind.Unwrap:
                    LayerField unwrapped = UnwrappedField(source, disposition, out object? value);
                    if (unwrapped.Type.Kind != LayerWireKind.Object)
                    {
                        throw new LayerUncoveredException("Only a root result can unwrap " +
                                                          source.Constructor.Name + "." +
                                                          unwrapped.Name + ".");
                    }
                    return TransformObject((LayerObjectValue)value!);
                case LayerDispositionKind.Unsupported:
                case LayerDispositionKind.ProvenNotEmitted:
                case LayerDispositionKind.NoTargetConstructor:
                case LayerDispositionKind.Omit:
                case LayerDispositionKind.Stream:
                    throw new LayerUncoveredException("Constructor disposition " +
                                                      disposition.Kind + " cannot transform " +
                                                      source.Constructor.Name + ".");
                default:
                    throw new LayerUncoveredException("Unknown constructor disposition.");
            }
        }

        if (!TargetSchema.TryGetConstructor(source.Constructor.Name,
                out LayerConstructor target) ||
            !source.Constructor.HasSameShape(target))
        {
            throw new LayerUncoveredException("Constructor is not covered: " +
                                              source.Constructor.Name + ".");
        }
        return TransformMechanical(source, source.Constructor.Name);
    }

    private LayerObjectValue TransformMechanical(LayerObjectValue source,
        string targetConstructorName)
    {
        return TransformCompatible(source, targetConstructorName, null);
    }

    private LayerObjectValue TransformCompatible(LayerObjectValue source,
        string targetConstructorName,
        IReadOnlyDictionary<string, object?>? replacements)
    {
        LayerConstructor target = TargetSchema.GetConstructor(targetConstructorName);
        var fields = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (LayerField targetField in target.Fields)
        {
            if (targetField.Type.Kind == LayerWireKind.Flags)
            {
                continue;
            }

            if (replacements != null &&
                replacements.TryGetValue(targetField.Name, out object? replacement))
            {
                fields.Add(targetField.Name, replacement);
                continue;
            }

            LayerField? sourceField = source.Constructor.FindField(targetField.Name);
            if (sourceField == null)
            {
                if (targetField.IsConditional)
                {
                    continue;
                }
                throw new LayerUncoveredException("Target field has no source field: " +
                                                  targetField.Name + ".");
            }
            if (!source.TryGetValue(sourceField.Name, out object? value))
            {
                if (targetField.IsConditional)
                {
                    continue;
                }
                throw new LayerUncoveredException("Required target field is absent: " +
                                                  targetField.Name + ".");
            }
            if (!sourceField.Type.HasSameWireType(targetField.Type))
            {
                throw new LayerUncoveredException("Target field has an incompatible source field: " +
                                                  targetField.Name + ".");
            }
            fields.Add(targetField.Name,
                TransformValue(sourceField.Type, targetField.Type, value));
        }
        return new LayerObjectValue(target, fields);
    }

    private bool IsOmitted(object? value)
    {
        return value is LayerObjectValue item &&
               _dispositions.TryGetValue(item.Constructor.Name,
                   out LayerConstructorDisposition? disposition) &&
               disposition.Kind == LayerDispositionKind.Omit;
    }

    private object? TransformValue(LayerType sourceType, LayerType targetType,
        object? value)
    {
        if (!sourceType.HasSameWireType(targetType))
        {
            throw new LayerUncoveredException("Mechanical value types differ.");
        }
        if (value == null)
        {
            return null;
        }
        if (sourceType.Kind == LayerWireKind.Object)
        {
            return TransformObject((LayerObjectValue)value);
        }
        if (sourceType.Kind == LayerWireKind.Vector)
        {
            var sourceValues = (IReadOnlyList<object?>)value;
            var targetValues = new List<object?>(sourceValues.Count);
            foreach (object? sourceValue in sourceValues)
            {
                if (!IsOmitted(sourceValue))
                {
                    targetValues.Add(TransformValue(sourceType.ElementType!,
                        targetType.ElementType!, sourceValue));
                }
            }
            return targetValues.AsReadOnly();
        }
        return value;
    }
}
