// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.TLParser
{
    public enum TokenType
    {
        HexConstant,
        LowercaseIdentifier,
        TypeIdentifier,
        Equal,
        EOL,
        Spaces,
        Dot,
        Semicolon,
        QuestionMark,
        ExclamationMark,
        Hash,
        Percent,
        Langle,
        Rangle,
        Colon,
        OpenBracket,
        CloseBracket,
        OpenBrace,
        CloseBrace,
        Functions,
        Types,
        EOF,
        None,
        Number,
        Multiply,
        LineComment,
        MultilineComment,
        NamespaceIdentifier,
        CombinatorIdentifier,
        VariableIdentifier,
        TypeTerm,
        ConditionalIdentifier,
        BareTypeIdentifier,
        OpenParen,
        CloseParen,
        BackTick,
        Plus,
        Minus,
        Slash
    }
}