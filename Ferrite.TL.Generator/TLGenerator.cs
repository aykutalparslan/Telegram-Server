// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Ferrite.TL.Schema;
using Microsoft.CodeAnalysis;

namespace Ferrite.TL.Generator;

[Generator]
public class TLGenerator : ISourceGenerator
{
    public void Execute(GeneratorExecutionContext context)
    {
        TLSourceGenerator sourceGenerator = new TLSourceGenerator();
        foreach (AdditionalText t in context.AdditionalFiles
                     .OrderBy(t => SchemaOrder(t.Path))
                     .ThenBy(t => t.Path, StringComparer.Ordinal))
        {
            if (Path.GetExtension(t.Path).Equals(".tl", StringComparison.OrdinalIgnoreCase))
            {
                var name = Path.GetFileNameWithoutExtension(t.Path);
                context.ReportDiagnostic(Diagnostic.Create(
                    new DiagnosticDescriptor(
                        "TLGenerator001",
                        "Generation started.",
                        $"Generating code for TL Schema: {name}",
                        "CodeGen",
                        DiagnosticSeverity.Info,
                        true),null,default(object),null));
                try
                {
                    foreach (GeneratedSource generatedSource in sourceGenerator.Generate(name, 
                                 File.ReadAllText(t.Path)))
                    {
                        if (generatedSource != TLSourceGenerator.DefaultSource)
                        {
                            context.AddSource(generatedSource.Name, generatedSource.SourceText);
                        }
                    }
                }
                catch (Exception e)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        new DiagnosticDescriptor(
                            "TLGenerator001",
                            "Error occured.",
                            e.Message,
                            "CodeGen",
                            DiagnosticSeverity.Error,
                            true),null,default(object),null));
                }
            }
        }
        try
        {
            var objectReader = sourceGenerator.GenerateObjectReader();
            context.AddSource(objectReader.Name, objectReader.SourceText);
            var constructors = sourceGenerator.GenerateConstructors();
            context.AddSource(constructors.Name, constructors.SourceText);
        }
        catch (Exception e)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                new DiagnosticDescriptor(
                    "TLGenerator002",
                    "Error occured.",
                    e.Message,
                    "CodeGen",
                    DiagnosticSeverity.Error,
                    true),null,default(object),null));
        }
    }

    private static int SchemaOrder(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        return name switch
        {
            "mtproto" => -3,
            "baseLayer" => -2,
            "e2eChain" => -1,
            _ when name.StartsWith("layer", StringComparison.Ordinal) &&
                   int.TryParse(name.Substring(5), NumberStyles.None, CultureInfo.InvariantCulture,
                       out int layer) => layer,
            _ => int.MaxValue
        };
    }

    public void Initialize(GeneratorInitializationContext context)
    {
        
    }
}
