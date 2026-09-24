// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.TL.Schema;

public sealed class LayerConversionContext
{
    private readonly Func<LayerObjectValue, string,
        IReadOnlyDictionary<string, object?>?, IReadOnlyCollection<string>?,
        LayerObjectValue> _convertCompatible;
    private readonly Func<LayerObjectValue, LayerObjectValue> _convert;

    public LayerSchema SourceSchema { get; }
    public LayerSchema TargetSchema { get; }

    internal LayerConversionContext(LayerSchema sourceSchema, LayerSchema targetSchema,
        Func<LayerObjectValue, string, IReadOnlyDictionary<string, object?>?,
            IReadOnlyCollection<string>?, LayerObjectValue> convertCompatible,
        Func<LayerObjectValue, LayerObjectValue> convert)
    {
        SourceSchema = sourceSchema;
        TargetSchema = targetSchema;
        _convertCompatible = convertCompatible;
        _convert = convert;
    }

    public LayerObjectValue ConvertCompatible(LayerObjectValue source,
        string targetConstructor,
        IReadOnlyDictionary<string, object?>? replacements = null,
        IReadOnlyCollection<string>? consumed = null)
    {
        return _convertCompatible(source, targetConstructor, replacements, consumed);
    }

    public LayerObjectValue Convert(LayerObjectValue source)
    {
        return _convert(source);
    }

    public LayerObjectValue Create(string targetConstructor,
        IEnumerable<KeyValuePair<string, object?>>? fields = null)
    {
        return new LayerObjectValue(TargetSchema.GetConstructor(targetConstructor),
            fields ?? Array.Empty<KeyValuePair<string, object?>>());
    }

    public void CopyField(LayerObjectValue source,
        IDictionary<string, object?> target, string field)
    {
        if (source.TryGetValue(field, out object? value))
        {
            target.Add(field, CopyValue(value));
        }
    }

    public IReadOnlyList<object?> ConvertElements(LayerObjectValue source, string field,
        Func<LayerConversionContext, LayerObjectValue, LayerObjectValue?> converter)
    {
        IReadOnlyList<object?> elements = source.Get<IReadOnlyList<object?>>(field);
        var converted = new object?[elements.Count];
        for (int index = 0; index < elements.Count; index++)
        {
            if (elements[index] is not LayerObjectValue element)
            {
                throw new InvalidOperationException("The field " + field +
                                                    " is malformed.");
            }
            converted[index] = converter(this, element);
        }
        return Array.AsReadOnly(converted);
    }

    private object? CopyValue(object? value)
    {
        if (value is LayerObjectValue nested)
        {
            return Convert(nested);
        }
        if (value is IReadOnlyList<object?> elements)
        {
            var copied = new object?[elements.Count];
            for (int index = 0; index < elements.Count; index++)
            {
                copied[index] = CopyValue(elements[index]);
            }
            return Array.AsReadOnly(copied);
        }
        return value;
    }
}
