// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.TLParser
{
    public struct Token
    {
        public TokenType Type { get; set; }
        public string? Value { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
    }
}