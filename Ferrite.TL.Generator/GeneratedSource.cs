// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.TL.Generator;

public class GeneratedSource
{
    public GeneratedSource(string name, string sourceText)
    {
        Name = name;
        SourceText = sourceText;
    }
    public string Name { get; set; }
    public string SourceText { get; set; }
}