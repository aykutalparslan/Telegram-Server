// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.TL.Schema;

public sealed class LayerRequestDefault
{
    public LayerEdge Edge { get; }
    public string Owner { get; }
    public string Field { get; }
    public object Value { get; }

    public LayerRequestDefault(LayerEdge edge, string owner, string field, object value)
    {
        Edge = edge;
        Owner = owner ?? throw new ArgumentNullException(nameof(owner));
        Field = field ?? throw new ArgumentNullException(nameof(field));
        Value = value ?? throw new ArgumentNullException(nameof(value));
    }
}

public sealed class LayerRequestConverterDeclaration
{
    public LayerEdge Edge { get; }
    public bool IsMethod { get; }
    public string Source { get; }
    public LayerRequestConverter Converter { get; }

    public LayerRequestConverterDeclaration(LayerEdge edge, bool isMethod, string source,
        LayerRequestConverter converter)
    {
        Edge = edge;
        IsMethod = isMethod;
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Converter = converter ?? throw new ArgumentNullException(nameof(converter));
    }
}
