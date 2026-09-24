// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Collections.ObjectModel;

namespace Ferrite.TL.Schema;

public enum LayerWireKind
{
    Int32,
    Int64,
    Double,
    Int128,
    Int256,
    Int512,
    Bytes,
    String,
    Flags,
    True,
    Object,
    Vector
}

public sealed class LayerType
{
    public LayerWireKind Kind { get; }
    public string? Name { get; }
    public bool IsBare { get; }
    public LayerType? ElementType { get; }

    public LayerType(LayerWireKind kind, string? name = null,
        bool isBare = false, LayerType? elementType = null)
    {
        Kind = kind;
        Name = name;
        IsBare = isBare;
        ElementType = elementType;
    }

    public bool HasSameWireType(LayerType other)
    {
        if (Kind != other.Kind || IsBare != other.IsBare ||
            !string.Equals(Name, other.Name, StringComparison.Ordinal))
        {
            return false;
        }

        if (ElementType == null || other.ElementType == null)
        {
            return ElementType == null && other.ElementType == null;
        }

        return ElementType.HasSameWireType(other.ElementType);
    }
}

public sealed class LayerField
{
    public string Name { get; }
    public LayerType Type { get; }
    public string? FlagsField { get; }
    public int? FlagBit { get; }

    public bool IsConditional => FlagsField != null && FlagBit != null;

    public LayerField(string name, LayerType type,
        string? flagsField = null, int? flagBit = null)
    {
        Name = name;
        Type = type;
        FlagsField = flagsField;
        FlagBit = flagBit;
    }
}

public sealed class LayerConstructor
{
    private readonly IReadOnlyList<LayerField> _fields;

    public string Name { get; }
    public int Id { get; }
    public string ResultType { get; }
    public IReadOnlyList<LayerField> Fields => _fields;

    public LayerConstructor(string name, int id, string resultType,
        IEnumerable<LayerField> fields)
    {
        Name = name;
        Id = id;
        ResultType = resultType;
        _fields = Array.AsReadOnly(fields.ToArray());
    }

    public LayerField? FindField(string name)
    {
        for (int i = 0; i < _fields.Count; i++)
        {
            if (string.Equals(_fields[i].Name, name, StringComparison.Ordinal))
            {
                return _fields[i];
            }
        }

        return null;
    }

    public bool HasSameShape(LayerConstructor other)
    {
        if (Id != other.Id ||
            !string.Equals(Name, other.Name, StringComparison.Ordinal) ||
            !string.Equals(ResultType, other.ResultType, StringComparison.Ordinal) ||
            _fields.Count != other._fields.Count)
        {
            return false;
        }

        for (int i = 0; i < _fields.Count; i++)
        {
            LayerField left = _fields[i];
            LayerField right = other._fields[i];
            if (!string.Equals(left.Name, right.Name, StringComparison.Ordinal) ||
                !left.Type.HasSameWireType(right.Type) ||
                !string.Equals(left.FlagsField, right.FlagsField,
                    StringComparison.Ordinal) ||
                left.FlagBit != right.FlagBit)
            {
                return false;
            }
        }

        return true;
    }
}

public sealed class LayerSchema
{
    private readonly IReadOnlyList<LayerConstructor> _constructors;
    private readonly IReadOnlyDictionary<int, LayerConstructor> _byId;
    private readonly IReadOnlyDictionary<string, LayerConstructor> _byName;
    private readonly IReadOnlyDictionary<string, IReadOnlyList<LayerConstructor>> _byResultType;
    private readonly IReadOnlyDictionary<int, string> _vectorResults;
    private readonly IReadOnlyList<LayerConstructor> _methods;
    private readonly IReadOnlyDictionary<int, LayerConstructor> _methodsById;
    private readonly IReadOnlyDictionary<string, LayerConstructor> _methodsByName;

    public int Layer { get; }
    public IReadOnlyList<LayerConstructor> Constructors => _constructors;
    public IReadOnlyList<LayerConstructor> Methods => _methods;
    public IReadOnlyDictionary<int, string> VectorResults => _vectorResults;

    public LayerSchema(int layer, IEnumerable<LayerConstructor> constructors,
        IEnumerable<KeyValuePair<int, string>>? vectorResults = null,
        IEnumerable<LayerConstructor>? methods = null)
    {
        Layer = layer;
        LayerConstructor[] values = constructors.ToArray();
        var byId = new Dictionary<int, LayerConstructor>();
        var byName = new Dictionary<string, LayerConstructor>(StringComparer.Ordinal);
        var byResult = new Dictionary<string, List<LayerConstructor>>(StringComparer.Ordinal);
        foreach (LayerConstructor constructor in values)
        {
            if (byId.ContainsKey(constructor.Id))
            {
                throw new ArgumentException("Duplicate constructor id.", nameof(constructors));
            }
            if (byName.ContainsKey(constructor.Name))
            {
                throw new ArgumentException("Duplicate constructor name.", nameof(constructors));
            }
            byId.Add(constructor.Id, constructor);
            byName.Add(constructor.Name, constructor);
            if (!byResult.TryGetValue(constructor.ResultType, out List<LayerConstructor>? result))
            {
                result = new List<LayerConstructor>();
                byResult.Add(constructor.ResultType, result);
            }
            result.Add(constructor);
        }

        var readOnlyResult = new Dictionary<string, IReadOnlyList<LayerConstructor>>(
            StringComparer.Ordinal);
        foreach (KeyValuePair<string, List<LayerConstructor>> pair in byResult)
        {
            readOnlyResult.Add(pair.Key, Array.AsReadOnly(pair.Value.ToArray()));
        }

        _constructors = Array.AsReadOnly(values);
        _byId = new ReadOnlyDictionary<int, LayerConstructor>(byId);
        _byName = new ReadOnlyDictionary<string, LayerConstructor>(byName);
        _byResultType = new ReadOnlyDictionary<string, IReadOnlyList<LayerConstructor>>(
            readOnlyResult);

        var vectors = new Dictionary<int, string>();
        if (vectorResults != null)
        {
            foreach (KeyValuePair<int, string> entry in vectorResults)
            {
                vectors.Add(entry.Key, entry.Value);
            }
        }
        _vectorResults = new ReadOnlyDictionary<int, string>(vectors);

        LayerConstructor[] methodValues = methods == null
            ? Array.Empty<LayerConstructor>()
            : methods.ToArray();
        var methodsById = new Dictionary<int, LayerConstructor>();
        var methodsByName = new Dictionary<string, LayerConstructor>(StringComparer.Ordinal);
        foreach (LayerConstructor method in methodValues)
        {
            if (methodsById.ContainsKey(method.Id))
            {
                throw new ArgumentException("Duplicate method id.", nameof(methods));
            }
            if (methodsByName.ContainsKey(method.Name))
            {
                throw new ArgumentException("Duplicate method name.", nameof(methods));
            }
            methodsById.Add(method.Id, method);
            methodsByName.Add(method.Name, method);
        }

        _methods = Array.AsReadOnly(methodValues);
        _methodsById = new ReadOnlyDictionary<int, LayerConstructor>(methodsById);
        _methodsByName = new ReadOnlyDictionary<string, LayerConstructor>(methodsByName);
    }

    public bool TryGetMethod(int id, out LayerConstructor method)
    {
        return _methodsById.TryGetValue(id, out method!);
    }

    public bool TryGetMethod(string name, out LayerConstructor method)
    {
        return _methodsByName.TryGetValue(name, out method!);
    }

    public LayerConstructor GetMethod(string name)
    {
        if (!_methodsByName.TryGetValue(name, out LayerConstructor? method))
        {
            throw new ArgumentException("Unknown method.", nameof(name));
        }
        return method;
    }

    public bool TryGetVectorResultElement(int methodId, out string elementType)
    {
        return _vectorResults.TryGetValue(methodId, out elementType!);
    }

    public bool TryGetConstructor(int id, out LayerConstructor constructor)
    {
        return _byId.TryGetValue(id, out constructor!);
    }

    public bool TryGetConstructor(string name, out LayerConstructor constructor)
    {
        return _byName.TryGetValue(name, out constructor!);
    }

    public LayerConstructor GetConstructor(string name)
    {
        if (!_byName.TryGetValue(name, out LayerConstructor? constructor))
        {
            throw new ArgumentException("Unknown constructor.", nameof(name));
        }
        return constructor;
    }

    public bool TryGetBareConstructor(string resultType,
        out LayerConstructor constructor)
    {
        constructor = null!;
        return _byResultType.TryGetValue(resultType, out IReadOnlyList<LayerConstructor>? values) &&
               values.Count == 1 &&
               (constructor = values[0]) != null;
    }
}

public sealed class LayerObjectValue
{
    private readonly IReadOnlyDictionary<string, object?> _fields;

    public LayerConstructor Constructor { get; }
    public IReadOnlyDictionary<string, object?> Fields => _fields;

    public LayerObjectValue(LayerConstructor constructor,
        IEnumerable<KeyValuePair<string, object?>> fields)
    {
        Constructor = constructor;
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, object?> field in fields)
        {
            values.Add(field.Key, field.Value);
        }
        _fields = new ReadOnlyDictionary<string, object?>(values);
    }

    public LayerObjectValue(LayerConstructor constructor,
        IReadOnlyDictionary<string, object?> fields)
        : this(constructor, fields.Select(x => x))
    {
    }

    public bool TryGetValue(string name, out object? value)
    {
        return _fields.TryGetValue(name, out value);
    }

    public T Get<T>(string name)
    {
        if (!TryGetValue(name, out object? value) || value is not T typed)
        {
            throw new InvalidOperationException("The field " + name + " is absent.");
        }
        return typed;
    }
}
