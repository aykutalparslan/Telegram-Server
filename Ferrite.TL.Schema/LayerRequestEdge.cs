// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Collections.ObjectModel;

namespace Ferrite.TL.Schema;

public sealed class LayerRequestEdge
{
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, object>> _defaults;
    private readonly IReadOnlyDictionary<string, LayerRequestConverter> _methodConverters;
    private readonly IReadOnlyDictionary<string, LayerRequestConverter> _constructorConverters;

    public LayerEdge Edge { get; }
    public LayerSchema SourceSchema { get; }
    public LayerSchema TargetSchema { get; }
    public IReadOnlyList<LayerRequestDefault> Defaults { get; }
    public IReadOnlyList<LayerRequestConverterDeclaration> Converters { get; }

    public LayerRequestEdge(LayerSchema sourceSchema, LayerSchema targetSchema,
        IEnumerable<LayerRequestDefault>? defaults = null,
        IEnumerable<LayerRequestConverterDeclaration>? converters = null)
    {
        if (sourceSchema.Layer == targetSchema.Layer)
        {
            throw new ArgumentException("An edge must join different layers.");
        }

        SourceSchema = sourceSchema;
        TargetSchema = targetSchema;
        Edge = new LayerEdge(sourceSchema.Layer, targetSchema.Layer);

        LayerRequestDefault[] defaultValues = defaults == null
            ? Array.Empty<LayerRequestDefault>()
            : defaults.ToArray();
        LayerRequestConverterDeclaration[] converterValues = converters == null
            ? Array.Empty<LayerRequestConverterDeclaration>()
            : converters.ToArray();
        _defaults = IndexDefaults(defaultValues);
        var methodConverters = new Dictionary<string, LayerRequestConverter>(
            StringComparer.Ordinal);
        var constructorConverters = new Dictionary<string, LayerRequestConverter>(
            StringComparer.Ordinal);
        IndexConverters(converterValues, methodConverters, constructorConverters);
        _methodConverters = new ReadOnlyDictionary<string, LayerRequestConverter>(
            methodConverters);
        _constructorConverters = new ReadOnlyDictionary<string, LayerRequestConverter>(
            constructorConverters);
        Defaults = Array.AsReadOnly(defaultValues);
        Converters = Array.AsReadOnly(converterValues);
    }

    public LayerObjectValue MapMethod(LayerObjectValue source)
    {
        if (_methodConverters.TryGetValue(source.Constructor.Name,
                out LayerRequestConverter? converter))
        {
            LayerObjectValue converted = Convert(converter, source.Constructor.Name,
                source);
            if (!TargetSchema.TryGetMethod(converted.Constructor.Name, out _))
            {
                throw new LayerUncoveredException("Request converter for " +
                                                  source.Constructor.Name +
                                                  " produced an unknown method at layer " +
                                                  TargetSchema.Layer + ".");
            }
            return converted;
        }
        if (!TargetSchema.TryGetMethod(source.Constructor.Name,
                out LayerConstructor target))
        {
            throw new LayerUncoveredException("Method is absent at layer " +
                                              TargetSchema.Layer + ": " +
                                              source.Constructor.Name + ".");
        }
        return MapFields(source, target, null, null);
    }

    private IReadOnlyDictionary<string, IReadOnlyDictionary<string, object>> IndexDefaults(
        IEnumerable<LayerRequestDefault> values)
    {
        var byOwner = new Dictionary<string, Dictionary<string, object>>(
            StringComparer.Ordinal);
        foreach (LayerRequestDefault value in values)
        {
            string name = value.Owner + "." + value.Field;
            if (!value.Edge.Equals(Edge))
            {
                throw new ArgumentException("A declared default names another edge: " +
                                            name + ".", nameof(values));
            }
            if (!TryGetSymbol(TargetSchema, value.Owner, out LayerConstructor owner) ||
                !TryGetSymbol(SourceSchema, value.Owner, out LayerConstructor source))
            {
                throw new ArgumentException("A declared default names an unknown owner: " +
                                            value.Owner + ".", nameof(values));
            }
            LayerField field = owner.FindField(value.Field) ??
                               throw new ArgumentException(
                                   "A declared default names an unknown field: " + name +
                                   ".", nameof(values));
            if (field.IsConditional)
            {
                throw new ArgumentException("A declared default names a conditional field: " +
                                            name + ".", nameof(values));
            }
            if (source.FindField(value.Field) is { IsConditional: false })
            {
                throw new ArgumentException(
                    "A declared default names a field the source layer always carries: " +
                    name + ".", nameof(values));
            }
            if (!Accepts(field.Type, value.Value))
            {
                throw new ArgumentException("A declared default has the wrong type: " +
                                            name + ".", nameof(values));
            }
            if (!byOwner.TryGetValue(value.Owner, out Dictionary<string, object>? fields))
            {
                fields = new Dictionary<string, object>(StringComparer.Ordinal);
                byOwner.Add(value.Owner, fields);
            }
            if (fields.ContainsKey(value.Field))
            {
                throw new ArgumentException("Duplicate declared default for " + name + ".",
                    nameof(values));
            }
            fields.Add(value.Field, value.Value);
        }

        var readOnly = new Dictionary<string, IReadOnlyDictionary<string, object>>(
            StringComparer.Ordinal);
        foreach (KeyValuePair<string, Dictionary<string, object>> entry in byOwner)
        {
            readOnly.Add(entry.Key, new ReadOnlyDictionary<string, object>(entry.Value));
        }
        return new ReadOnlyDictionary<string, IReadOnlyDictionary<string, object>>(readOnly);
    }

    private void IndexConverters(IEnumerable<LayerRequestConverterDeclaration> declarations,
        Dictionary<string, LayerRequestConverter> methods,
        Dictionary<string, LayerRequestConverter> constructors)
    {
        foreach (LayerRequestConverterDeclaration declaration in declarations)
        {
            if (!declaration.Edge.Equals(Edge))
            {
                throw new ArgumentException("A declared converter names another edge: " +
                                            declaration.Source + ".", nameof(declarations));
            }
            bool known = declaration.IsMethod
                ? SourceSchema.TryGetMethod(declaration.Source, out _)
                : SourceSchema.TryGetConstructor(declaration.Source, out _);
            if (!known)
            {
                throw new ArgumentException("A declared converter names an unknown " +
                                            (declaration.IsMethod ? "method" : "constructor") +
                                            ": " + declaration.Source + ".",
                    nameof(declarations));
            }
            Dictionary<string, LayerRequestConverter> target =
                declaration.IsMethod ? methods : constructors;
            if (target.ContainsKey(declaration.Source))
            {
                throw new ArgumentException("Duplicate declared converter for " +
                                            declaration.Source + ".", nameof(declarations));
            }
            target.Add(declaration.Source, declaration.Converter);
        }
    }

    private LayerObjectValue MapFields(LayerObjectValue source, LayerConstructor target,
        IReadOnlyDictionary<string, object?>? replacements,
        IReadOnlyCollection<string>? converterConsumed)
    {
        LayerConstructor sourceConstructor = source.Constructor;
        _defaults.TryGetValue(target.Name,
            out IReadOnlyDictionary<string, object>? declared);

        var consumed = new HashSet<string>(StringComparer.Ordinal);
        if (converterConsumed != null)
        {
            consumed.UnionWith(converterConsumed);
        }

        var applied = new HashSet<string>(StringComparer.Ordinal);
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
                applied.Add(targetField.Name);
                consumed.Add(targetField.Name);
                if (replacement != null)
                {
                    fields.Add(targetField.Name, replacement);
                    continue;
                }
                if (targetField.IsConditional)
                {
                    continue;
                }
                throw Absent(target, targetField);
            }

            LayerField? sourceField = sourceConstructor.FindField(targetField.Name);
            if (sourceField != null)
            {
                consumed.Add(sourceField.Name);
                if (!sourceField.Type.HasSameWireType(targetField.Type))
                {
                    throw new LayerUncoveredException("Field changes its wire type at layer " +
                                                      TargetSchema.Layer + ": " +
                                                      target.Name + "." + targetField.Name +
                                                      ".");
                }
                if (source.TryGetValue(sourceField.Name, out object? value))
                {
                    fields.Add(targetField.Name,
                        MapValue(sourceField.Type, targetField.Type, value));
                    continue;
                }
            }

            if (declared != null &&
                declared.TryGetValue(targetField.Name, out object? fallback))
            {
                fields.Add(targetField.Name, fallback);
                continue;
            }
            if (targetField.IsConditional)
            {
                continue;
            }
            throw Absent(target, targetField);
        }

        if (replacements != null)
        {
            foreach (string name in replacements.Keys)
            {
                if (!applied.Contains(name))
                {
                    throw new LayerUncoveredException(
                        "A replacement names a field layer " + TargetSchema.Layer +
                        " lacks: " + target.Name + "." + name + ".");
                }
            }
        }

        foreach (LayerField sourceField in sourceConstructor.Fields)
        {
            if (sourceField.Type.Kind == LayerWireKind.Flags ||
                consumed.Contains(sourceField.Name))
            {
                continue;
            }
            if (source.Fields.ContainsKey(sourceField.Name))
            {
                throw new LayerUncoveredException("Field is dropped at layer " +
                                                  TargetSchema.Layer + ": " +
                                                  sourceConstructor.Name + "." +
                                                  sourceField.Name + ".");
            }
        }

        return new LayerObjectValue(target, fields);
    }

    private LayerUncoveredException Absent(LayerConstructor target, LayerField field)
    {
        return new LayerUncoveredException("Required field is absent at layer " +
                                           TargetSchema.Layer + ": " + target.Name + "." +
                                           field.Name + ".");
    }

    private LayerObjectValue MapCompatible(LayerObjectValue source, string targetName,
        IReadOnlyDictionary<string, object?>? replacements,
        IReadOnlyCollection<string>? converterConsumed)
    {
        if (!TryGetSymbol(TargetSchema, targetName, out LayerConstructor target))
        {
            throw new LayerUncoveredException("Symbol is absent at layer " +
                                              TargetSchema.Layer + ": " + targetName + ".");
        }
        return MapFields(source, target, replacements, converterConsumed);
    }

    private LayerObjectValue Convert(LayerRequestConverter converter, string source,
        LayerObjectValue value)
    {
        LayerObjectValue? converted;
        try
        {
            converted = converter(new LayerConversionContext(SourceSchema, TargetSchema,
                MapCompatible, MapObject), value);
        }
        catch (Exception exception)
        {
            throw new LayerUncoveredException("Request converter for " + source +
                                              " failed: " + exception.Message);
        }
        if (converted == null)
        {
            throw new LayerUncoveredException("Request converter for " + source +
                                              " did not produce a value.");
        }
        return converted;
    }

    private LayerObjectValue MapObject(LayerObjectValue source)
    {
        if (_constructorConverters.TryGetValue(source.Constructor.Name,
                out LayerRequestConverter? converter))
        {
            LayerObjectValue converted = Convert(converter, source.Constructor.Name,
                source);
            if (!TargetSchema.TryGetConstructor(converted.Constructor.Name, out _))
            {
                throw new LayerUncoveredException("Request converter for " +
                                                  source.Constructor.Name +
                                                  " produced an unknown constructor at layer " +
                                                  TargetSchema.Layer + ".");
            }
            return converted;
        }
        if (!TargetSchema.TryGetConstructor(source.Constructor.Name,
                out LayerConstructor target))
        {
            throw new LayerUncoveredException("Constructor is absent at layer " +
                                              TargetSchema.Layer + ": " +
                                              source.Constructor.Name + ".");
        }
        return MapFields(source, target, null, null);
    }

    private object? MapValue(LayerType sourceType, LayerType targetType, object? value)
    {
        if (value == null)
        {
            return null;
        }
        if (sourceType.Kind == LayerWireKind.Object)
        {
            return MapObject((LayerObjectValue)value);
        }
        if (sourceType.Kind == LayerWireKind.Vector)
        {
            var sourceValues = (IReadOnlyList<object?>)value;
            var targetValues = new object?[sourceValues.Count];
            for (int i = 0; i < sourceValues.Count; i++)
            {
                targetValues[i] = MapValue(sourceType.ElementType!,
                    targetType.ElementType!, sourceValues[i]);
            }
            return Array.AsReadOnly(targetValues);
        }
        return value;
    }

    private static bool TryGetSymbol(LayerSchema schema, string name,
        out LayerConstructor symbol)
    {
        return schema.TryGetMethod(name, out symbol) ||
               schema.TryGetConstructor(name, out symbol);
    }

    private static bool Accepts(LayerType type, object value)
    {
        switch (type.Kind)
        {
            case LayerWireKind.Int32:
                return value is int;
            case LayerWireKind.Int64:
                return value is long;
            case LayerWireKind.Double:
                return value is double;
            case LayerWireKind.Bytes:
            case LayerWireKind.String:
                return value is byte[];
            case LayerWireKind.True:
                return value is bool flag && flag;
            default:
                return false;
        }
    }
}
