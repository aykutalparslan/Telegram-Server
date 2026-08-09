// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Collections.Generic;

namespace Ferrite.TLParser;

public class CombinatorDeclarationSyntax
{
    public string? ContainingNamespace { get; set; }
    public string? Namespace { get; set; }
    public string? Identifier { get; set; }
    public CombinatorType CombinatorType { get; set; }
    public IReadOnlyList<OptionalArgumentSyntax>? OptionalArguments { get; set; }
    public IReadOnlyList<SimpleArgumentSyntax>? Arguments { get; set; }
    public string? Name { get; set; }
    public int? Multiply { get; set; }
    public TypeTermSyntax? Type { get; set; }
}