// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Globalization;
using System.Text;
using Ferrite.TL.Schema;

namespace Ferrite.TL.Generator;

public sealed class LayerEdgeSourceGenerator
{
    public LayerTransformEdgeDefinition BuildDefinition(int sourceLayer,
        int targetLayer, string sourceSchema, string targetSchema,
        IEnumerable<LayerConstructorDisposition> dispositions,
        IReadOnlyDictionary<string, LayerSemanticConverter>? converters = null)
    {
        return new LayerTransformEdgeDefinition(
            ParseSchema(sourceLayer, sourceSchema),
            ParseSchema(targetLayer, targetSchema),
            dispositions,
            converters);
    }

    public GeneratedSource Generate(string nameSpace, string className,
        int sourceLayer, int targetLayer, string sourceSchema,
        string targetSchema,
        IEnumerable<LayerConstructorDisposition> dispositions)
    {
        LayerTransformEdgeDefinition definition = BuildDefinition(sourceLayer,
            targetLayer, sourceSchema, targetSchema, dispositions);
        var builder = new StringBuilder();
        builder.Append("#nullable enable\n\nnamespace ")
            .Append(nameSpace)
            .Append(";\n\npublic static class ")
            .Append(className)
            .Append("\n{\n    public static global::Ferrite.TL.Schema.LayerTransformEdgeDefinition Create(\n        global::System.Collections.Generic.IReadOnlyDictionary<string, global::Ferrite.TL.Schema.LayerSemanticConverter>? converters = null)\n    {\n        return new global::Ferrite.TL.Schema.LayerTransformEdgeDefinition(\n");
        AppendSchema(builder, definition.SourceSchema, "            ");
        builder.Append(",\n");
        AppendSchema(builder, definition.TargetSchema, "            ");
        builder.Append(",\n            new global::Ferrite.TL.Schema.LayerConstructorDisposition[]\n            {\n");
        foreach (LayerConstructorDisposition disposition in definition.Dispositions)
        {
            builder.Append("                new global::Ferrite.TL.Schema.LayerConstructorDisposition(\"")
                .Append(Escape(disposition.SourceConstructor))
                .Append("\", global::Ferrite.TL.Schema.LayerDispositionKind.")
                .Append(disposition.Kind);
            if (disposition.TargetConstructor != null || disposition.ConverterName != null)
            {
                builder.Append(", ")
                    .Append(StringLiteral(disposition.TargetConstructor));
            }
            if (disposition.ConverterName != null)
            {
                builder.Append(", ")
                    .Append(StringLiteral(disposition.ConverterName));
            }
            if (disposition.Field != null)
            {
                builder.Append(", field: ")
                    .Append(StringLiteral(disposition.Field));
            }
            builder.Append("),\n");
        }
        builder.Append("            },\n            converters);\n    }\n}\n");
        return new GeneratedSource(className + ".g.cs", builder.ToString());
    }

    public LayerSchema ParseSchema(int layer, string source)
    {
        return LayerSchemaReader.Read(layer, source);
    }

    private static void AppendSchema(StringBuilder builder, LayerSchema schema,
        string indent)
    {
        builder.Append(indent)
            .Append("new global::Ferrite.TL.Schema.LayerSchema(")
            .Append(schema.Layer)
            .Append(", new global::Ferrite.TL.Schema.LayerConstructor[]\n")
            .Append(indent)
            .Append("{\n");
        foreach (LayerConstructor constructor in schema.Constructors)
        {
            builder.Append(indent)
                .Append("    new global::Ferrite.TL.Schema.LayerConstructor(\"")
                .Append(Escape(constructor.Name))
                .Append("\", unchecked((int)0x")
                .Append(unchecked((uint)constructor.Id).ToString("x8",
                    CultureInfo.InvariantCulture))
                .Append("), \"")
                .Append(Escape(constructor.ResultType))
                .Append("\", new global::Ferrite.TL.Schema.LayerField[]\n")
                .Append(indent)
                .Append("    {\n");
            foreach (LayerField field in constructor.Fields)
            {
                builder.Append(indent)
                    .Append("        new global::Ferrite.TL.Schema.LayerField(\"")
                    .Append(Escape(field.Name))
                    .Append("\", ");
                AppendType(builder, field.Type);
                if (field.FlagsField != null)
                {
                    builder.Append(", \"")
                        .Append(Escape(field.FlagsField))
                        .Append("\", ")
                        .Append(field.FlagBit);
                }
                builder.Append("),\n");
            }
            builder.Append(indent)
                .Append("    }),\n");
        }
        builder.Append(indent).Append("})");
    }

    private static void AppendType(StringBuilder builder, LayerType type)
    {
        builder.Append("new global::Ferrite.TL.Schema.LayerType(global::Ferrite.TL.Schema.LayerWireKind.")
            .Append(type.Kind);
        if (type.Name != null || type.IsBare || type.ElementType != null)
        {
            builder.Append(", ").Append(StringLiteral(type.Name));
        }
        if (type.IsBare || type.ElementType != null)
        {
            builder.Append(", ").Append(type.IsBare ? "true" : "false");
        }
        if (type.ElementType != null)
        {
            builder.Append(", ");
            AppendType(builder, type.ElementType);
        }
        builder.Append(')');
    }

    private static string StringLiteral(string? value)
    {
        return value == null ? "null" : "\"" + Escape(value) + "\"";
    }

    private static string Escape(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
