// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.TLParser;

public class SimpleArgumentSyntax
{
    public string? Identifier { get; set; }
    public ConditionalDefinitionSyntax? ConditionalDefinition { get; set; }
    public TypeTermSyntax? TypeTerm { get; set; }
}