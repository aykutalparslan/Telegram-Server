// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Ferrite.TLParser;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Ferrite.TLParser;

//https://github.com/dotnet/roslyn-sdk/blob/main/samples/CSharp/SourceGenerators/SourceGeneratorSamples/MathsGenerator.cs#L467
[Generator]
public class TLGenerator : ISourceGenerator
{
    public void Execute(GeneratorExecutionContext context)
    {
        TLSourceGenerator sourceGenerator = new TLSourceGenerator();
        foreach (AdditionalText t in context.AdditionalFiles)
        {
            List<string> paths = new List<string>();
            
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
    public void Initialize(GeneratorInitializationContext context)
    {
        
    }
}