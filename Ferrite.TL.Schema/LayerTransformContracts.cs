// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.TL.Schema;

public struct LayerEdge : IEquatable<LayerEdge>
{
    public int Source { get; }
    public int Target { get; }

    public LayerEdge(int source, int target)
    {
        Source = source;
        Target = target;
    }

    public bool Equals(LayerEdge other)
    {
        return Source == other.Source && Target == other.Target;
    }

    public override bool Equals(object? obj)
    {
        return obj is LayerEdge other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (Source * 397) ^ Target;
        }
    }

    public override string ToString()
    {
        return Source + "→" + Target;
    }
}

public enum LayerDispositionKind
{
    Mechanical,
    Semantic,
    Unsupported,
    ProvenNotEmitted,
    Stream,
    NoTargetConstructor,
    Unwrap,
    Omit
}

public sealed class LayerConstructorDisposition
{
    public string SourceConstructor { get; }
    public LayerDispositionKind Kind { get; }
    public string? TargetConstructor { get; }
    public string? ConverterName { get; }
    public string? Field { get; }

    public LayerConstructorDisposition(string sourceConstructor,
        LayerDispositionKind kind, string? targetConstructor = null,
        string? converterName = null, string? field = null)
    {
        SourceConstructor = sourceConstructor;
        Kind = kind;
        TargetConstructor = targetConstructor;
        ConverterName = converterName;
        Field = field;
    }
}

public enum LayerTransformStatus
{
    Identity,
    Transformed,
    Uncovered,
    Malformed
}

public sealed class LayerTransformResult
{
    public LayerTransformStatus Status { get; }
    public byte[]? Data { get; }
    public LayerEdge? FailureEdge { get; }
    public int? Constructor { get; }
    public string? Detail { get; }
    public string? VectorElementType { get; }
    public bool IsSuccess => Status == LayerTransformStatus.Identity ||
                             Status == LayerTransformStatus.Transformed;

    private LayerTransformResult(LayerTransformStatus status, byte[]? data,
        LayerEdge? failureEdge, int? constructor, string? detail,
        string? vectorElementType = null)
    {
        Status = status;
        Data = data;
        FailureEdge = failureEdge;
        Constructor = constructor;
        Detail = detail;
        VectorElementType = vectorElementType;
    }

    public static LayerTransformResult Identity(byte[] data)
    {
        return new LayerTransformResult(LayerTransformStatus.Identity,
            data, null, ReadConstructor(data), null);
    }

    public static LayerTransformResult Transformed(byte[] data)
    {
        return new LayerTransformResult(LayerTransformStatus.Transformed,
            data, null, ReadConstructor(data), null);
    }

    public static LayerTransformResult TransformedVector(byte[] data,
        string vectorElementType)
    {
        return new LayerTransformResult(LayerTransformStatus.Transformed,
            data, null, ReadConstructor(data), null, vectorElementType);
    }

    public static LayerTransformResult Failure(LayerTransformStatus status,
        LayerEdge edge, int? constructor, string detail)
    {
        if (status != LayerTransformStatus.Uncovered &&
            status != LayerTransformStatus.Malformed)
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }
        return new LayerTransformResult(status, null, edge, constructor, detail);
    }

    public LayerTransformResult AtEdge(LayerEdge edge)
    {
        if (IsSuccess)
        {
            return this;
        }
        return new LayerTransformResult(Status, null, edge, Constructor, Detail);
    }

    private static int? ReadConstructor(byte[] data)
    {
        if (data.Length < 4)
        {
            return null;
        }
        return data[0] | data[1] << 8 | data[2] << 16 | data[3] << 24;
    }
}

public delegate LayerObjectValue? LayerSemanticConverter(
    LayerConversionContext context, LayerObjectValue source);

public delegate LayerObjectValue LayerRequestConverter(
    LayerConversionContext context, LayerObjectValue source);

public interface ILayerTransformEdge
{
    LayerEdge Edge { get; }
    LayerSchema SourceSchema { get; }
    LayerSchema TargetSchema { get; }
    bool HasUnsupportedDispositions { get; }
    IReadOnlyList<LayerConstructorDisposition> Dispositions { get; }
    LayerTransformResult Transform(byte[] source);
    LayerTransformResult Transform(byte[] source, string? vectorElementType);
}

public interface ILayerTransformRegistry
{
    int BaseLayer { get; }
    bool TryCreatePlan(int targetLayer, out LayerTransformPlan plan,
        out string? error);
}
