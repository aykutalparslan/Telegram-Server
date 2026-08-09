// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;

namespace Ferrite.TLParser;

public class TypeTermSyntax
{
    public bool IsBare { get; set; }
    public bool IsTypeOf { get; set; }
    public string? NamespaceIdentifier { get; set; }
    public string? Identifier { get; set; }
    public TypeTermSyntax? OptionalType { get; set; }

    public string GetFullyQualifiedIdentifier()
    {
        var sb = new StringBuilder();
        if (NamespaceIdentifier != null)
        {
            sb.Append(NamespaceIdentifier);
            sb.Append(".");
        }

        if (IsTypeOf)
        {
            return "BoxedObject";
        }
        if (Identifier == "Vector" && OptionalType?.Identifier == "int")
        {
            return "VectorOfInt";
        }
        if (Identifier == "Vector" && OptionalType?.Identifier == "long")
        {
            return "VectorOfLong";
        }
        if (Identifier == "Vector" && OptionalType?.Identifier == "double")
        {
            return "VectorOfDouble";
        }
        if (Identifier == "Vector" && (OptionalType?.Identifier == "string" || OptionalType?.Identifier == "bytes"))
        {
            return "VectorOfString";
        }
        // tde2e's `vector<...>` is a BARE vector: the reference writes only an
        // int32 count, never the 1cb5c415 constructor. Bare vectors of boxed
        // elements are served by VectorBare, but bare elements need their own
        // readers because there is no per-element constructor id to dispatch on.
        if (Identifier == "vector" && OptionalType?.Identifier == "long")
        {
            return "VectorBareOfLong";
        }
        if (Identifier == "vector" && (OptionalType?.Identifier == "string" || OptionalType?.Identifier == "bytes"))
        {
            return "VectorBareOfString";
        }

        if (Identifier == "vector")
        {
            sb.Append("VectorBare");
        }
        else if (Identifier is "bytes" or "string")
        {
            sb.Append("TLBytes");
        }
        else if (Identifier is "#")
        {
            sb.Append("Flags");
        }
        else if (Identifier is "Object")
        {
            sb.Append("BoxedObject");
        }
        else if (Identifier is "int128" or "int256" or "int512")
        {
            sb.Append("ReadOnlySpan<byte>");
        }
        else
        {
            sb.Append(Identifier);
        }
        //if (OptionalType == null) return sb.ToString();
        //sb.Append("<");
        //sb.Append(OptionalType.GetFullyQualifiedIdentifier());
        //sb.Append(">");
        return sb.ToString();
    }
}
