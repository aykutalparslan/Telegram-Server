// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Collections.Generic;

namespace Ferrite.TLParser;

public class Parser
{
    private readonly Lexer _lexer;
    private CombinatorType _combinatorType = CombinatorType.Constructor;

    public Parser(Lexer lexer)
    {
        _lexer = lexer;
    }

    public CombinatorDeclarationSyntax? ParseCombinator()
    {
        IReadOnlyList<Token> statement;
        while (true)
        {
            statement = NextStatement();
            if (statement.Count == 0)
            {
                return null;
            }

            if (statement[0].Type is TokenType.EOF)
            {
                return null;
            }

            if (statement[0].Type is TokenType.EOL)
            {
                continue;
            }

            if (statement[0].Type == TokenType.Functions)
            {
                _combinatorType = CombinatorType.Function;
                continue;
            }

            if (statement[0].Type == TokenType.Types)
            {
                _combinatorType = CombinatorType.Constructor;
                continue;
            }

            if (IsSkippableTdLibTestFunction(statement))
            {
                continue;
            }

            break;
        }

        int offset = 0;
        string? nameSpace = null;
        string? identifier = null;
        string? name = null;
        List<OptionalArgumentSyntax> optionalArguments = new();
        List<SimpleArgumentSyntax> arguments = new();
        if (statement.Count > 4 && statement[0].Type == TokenType.BareTypeIdentifier &&
            statement[1].Type == TokenType.QuestionMark &&
            statement[2].Type == TokenType.Equal &&
            statement[3].Type == TokenType.TypeIdentifier&&
            statement[4].Type == TokenType.Semicolon)
        {
            return new CombinatorDeclarationSyntax
            {
                Identifier = statement[0].Value,
                CombinatorType = CombinatorType.Builtin,
                Type = new TypeTermSyntax
                {
                    Identifier = statement[3].Value
                }
            };
        }
        if (statement.Count > 8 && statement[0].Type == TokenType.BareTypeIdentifier &&
            statement[1].Type == TokenType.Number &&
            statement[2].Type == TokenType.Multiply &&
            statement[3].Type == TokenType.OpenBracket&&
            statement[4].Type == TokenType.BareTypeIdentifier &&
            statement[5].Type == TokenType.CloseBracket &&
            statement[6].Type == TokenType.Equal &&
            statement[7].Type == TokenType.TypeIdentifier)
        {
            return new CombinatorDeclarationSyntax
            {
                Identifier = statement[0].Value,
                CombinatorType = CombinatorType.Builtin,
                Multiply = int.Parse(statement[1].Value), 
                Type = new TypeTermSyntax
                {
                    Identifier = statement[7].Value,
                    OptionalType = new TypeTermSyntax()
                    {
                        Identifier = statement[4].Value,
                    }
                }
            };
        }
        if (statement.Count > 7 && statement[0].Type == TokenType.NamespaceIdentifier &&
            statement[1].Type == TokenType.Dot &&
            statement[2].Type == TokenType.CombinatorIdentifier &&
            statement[3].Type == TokenType.Hash &&
            statement[4].Type == TokenType.HexConstant)
        {
            nameSpace = statement[0].Value;
            identifier = statement[2].Value;
            name = statement[4].Value;
            offset = 5;
        }
        else if (statement.Count > 6 && statement[0].Type == TokenType.CombinatorIdentifier &&
                 statement[1].Type == TokenType.Hash &&
                 statement[2].Type == TokenType.HexConstant)
        {
            identifier = statement[0].Value;
            name = statement[2].Value;
            offset = 3;
        }
        else if (statement.Count > 1 && statement[0].Type == TokenType.BareTypeIdentifier)
        {
            identifier = statement[0].Value;
            offset++;
        }
        else
        {
            return null;
        }

        while (ParseOptionalArgument(statement, offset, out var consumed) is { } argument)
        {
            optionalArguments.Add(argument);
            offset += consumed ;
        }

        if (optionalArguments.Count > 0 && statement.Count - offset > 7 &&
            statement[offset].Type == TokenType.Hash &&
            statement[offset + 1].Type == TokenType.OpenBracket &&
            statement[offset + 2].Type == TokenType.LowercaseIdentifier &&
            statement[offset + 3].Type == TokenType.CloseBracket &&
            statement[offset + 4].Type == TokenType.Equal &&
            statement[offset + 5].Type == TokenType.TypeIdentifier &&
            statement[offset + 6].Type == TokenType.LowercaseIdentifier &&
            statement[offset + 7].Type == TokenType.Semicolon)
        {
            return new CombinatorDeclarationSyntax
            {
                Identifier = identifier,
                OptionalArguments = optionalArguments,
                CombinatorType = CombinatorType.Builtin,
                Type = new TypeTermSyntax()
                {
                    Identifier = statement[offset + 5].Value,
                    OptionalType = optionalArguments[0].TypeTerm
                }
            };
        }

        while (ParseSimpleArgument(statement, offset, out var consumed) is { } argument)
        {
            arguments.Add(argument);
            offset += consumed;
        }

        if (statement.Count - offset > 4 && statement[offset].Type == TokenType.Equal &&
            statement[offset + 1].Type == TokenType.NamespaceIdentifier &&
            statement[offset + 2].Type == TokenType.Dot &&
            statement[offset + 3].Type == TokenType.TypeIdentifier &&
            statement[offset + 4].Type == TokenType.Semicolon &&
            statement[offset + 5].Type == TokenType.EOL)
        {
            return new CombinatorDeclarationSyntax()
            {
                Namespace = nameSpace,
                Identifier = identifier,
                Name = name,
                Arguments = arguments,
                OptionalArguments = optionalArguments,
                CombinatorType = _combinatorType,
                Type = new TypeTermSyntax()
                {
                    NamespaceIdentifier = statement[offset + 1].Value,
                    Identifier = statement[offset + 3].Value
                }
            };
        }
        
        if (statement.Count - offset > 4 && statement[offset].Type == TokenType.Equal &&
            statement[offset + 1].Type == TokenType.NamespaceIdentifier &&
            statement[offset + 2].Type == TokenType.Dot &&
            statement[offset + 3].Type == TokenType.TypeIdentifier &&
            statement[offset + 4].Type == TokenType.Langle)
        {
            var returnType = ParseTypeTerm(statement, offset+1, out var consumed);
            offset += consumed;
            return new CombinatorDeclarationSyntax()
            {
                Namespace = nameSpace,
                Identifier = identifier,
                Name = name,
                Arguments = arguments,
                OptionalArguments = optionalArguments,
                CombinatorType = _combinatorType,
                Type = returnType
            };
        }
        
        if (statement.Count - offset > 4 && statement[offset].Type == TokenType.Equal &&
            statement[offset + 1].Type == TokenType.TypeIdentifier &&
            statement[offset + 2].Type == TokenType.Langle)
        {
            var returnType = ParseTypeTerm(statement, offset+1, out var consumed);
            offset += consumed;
            return new CombinatorDeclarationSyntax()
            {
                Namespace = nameSpace,
                Identifier = identifier,
                Name = name,
                Arguments = arguments,
                OptionalArguments = optionalArguments,
                CombinatorType = _combinatorType,
                Type = returnType
            };
        }

        if (statement.Count - offset > 3 && statement[offset].Type == TokenType.Equal &&
            statement[offset + 1].Type == TokenType.TypeIdentifier &&
            statement[offset + 2].Type == TokenType.Semicolon &&
            statement[offset + 3].Type == TokenType.EOL)
        {
            return new CombinatorDeclarationSyntax()
            {
                Namespace = nameSpace,
                Identifier = identifier,
                Name = name,
                Arguments = arguments,
                OptionalArguments = optionalArguments,
                CombinatorType = _combinatorType,
                Type = new TypeTermSyntax()
                {
                    Identifier = statement[offset + 1].Value
                }
            };
        }

        return null;
    }

    private bool IsSkippableTdLibTestFunction(IReadOnlyList<Token> statement)
    {
        if (_combinatorType != CombinatorType.Function ||
            statement.Count < 4 ||
            statement[0].Type != TokenType.NamespaceIdentifier ||
            statement[0].Value != "test")
        {
            return false;
        }

        foreach (var token in statement)
        {
            if (token.Type == TokenType.Hash)
            {
                return false;
            }
        }

        return true;
    }

    private static OptionalArgumentSyntax? ParseOptionalArgument(IReadOnlyList<Token> statement, int offset, out int consumed)
    {
        if (statement.Count - offset > 4 && statement[offset].Type == TokenType.OpenBrace &&
            (statement[offset + 1].Type is TokenType.VariableIdentifier or TokenType.TypeIdentifier) &&
            statement[offset + 2].Type == TokenType.Colon &&
            statement[offset + 3].Type == TokenType.TypeIdentifier &&
            statement[offset + 4].Type == TokenType.CloseBrace)
        {
            consumed = 5;
            return new OptionalArgumentSyntax()
            {
                Identifier = statement[offset + 1].Value,
                TypeTerm = new TypeTermSyntax()
                {
                    Identifier = statement[offset + 3].Value,
                }
            };
        }
        consumed = 0;
        return null;
    }
    
    private static SimpleArgumentSyntax? ParseSimpleArgument(IReadOnlyList<Token> statement, int offset, out int consumed)
    {
        if (statement.Count - offset > 3 &&
            statement[offset].Type == TokenType.VariableIdentifier &&
            statement[offset + 1].Type == TokenType.Colon &&
            statement[offset + 2].Type == TokenType.Hash)
        {
            consumed = 3;
           return new SimpleArgumentSyntax()
            {
                Identifier = statement[offset].Value,
                TypeTerm = new TypeTermSyntax()
                {
                    Identifier = "#",
                },
            };
        }
        if (statement.Count - offset > 3 &&
            statement[offset].Type == TokenType.VariableIdentifier &&
            statement[offset + 1].Type == TokenType.Colon)
        {
            consumed = 2;
            var conditional = ParseConditionalDefinition(statement, offset + consumed, out var consumed2);
            consumed += consumed2;
            var type = ParseTypeTerm(statement, offset + consumed, out var consumed3);
            consumed += consumed3;
            return new SimpleArgumentSyntax()
            {
                Identifier = statement[offset].Value,
                ConditionalDefinition = conditional,
                TypeTerm = type,
            };
        }
        consumed = 0;
        return null;
    }

    private static ConditionalDefinitionSyntax? ParseConditionalDefinition(IReadOnlyList<Token> statement, int offset,
        out int consumed)
    {
        if (statement.Count - offset > 4 && statement[offset].Type == TokenType.ConditionalIdentifier &&
            statement[offset + 1].Type == TokenType.Dot &&
            statement[offset + 2].Type == TokenType.Number &&
            statement[offset + 3].Type == TokenType.QuestionMark)
        {
            consumed = 4;
            return new ConditionalDefinitionSyntax()
            {
                Identifier = statement[offset].Value,
                ConditionalArgumentBit = int.Parse(statement[offset + 2].Value)
            };
        }

        consumed = 0;
        return null;
    }
    
    private static TypeTermSyntax? ParseTypeTerm(IReadOnlyList<Token> statement, int offset, out int consumed)
    {
        consumed = 0;
        bool isBare = false;
        if (statement[offset].Type == TokenType.Percent)
        {
            offset++;
            consumed++;
            isBare = true;
        }
        bool isTypeOf = false;
        if (statement[offset].Type == TokenType.ExclamationMark)
        {
            offset++;
            consumed++;
            isTypeOf = true;
        }
        if (statement.Count - offset > 1 && statement[offset].Type == TokenType.BareTypeIdentifier &&
            statement[offset + 1].Type != TokenType.Langle)
        {
            consumed += 1;
            return new TypeTermSyntax()
            {
                IsBare = true,
                IsTypeOf = isTypeOf,
                Identifier = statement[offset].Value
            };
        }

        if (statement.Count - offset > 3 && statement[offset].Type == TokenType.BareTypeIdentifier &&
            statement[offset + 1].Type == TokenType.Langle)
        {
            consumed += 2;
            var innerTerm = ParseTypeTerm(statement, offset + consumed, out var consumed2);
            consumed += consumed2;
            if (statement[offset + consumed].Type == TokenType.Rangle)
            {
                consumed++;
                return new TypeTermSyntax()
                {
                    IsBare = true,
                    IsTypeOf = isTypeOf,
                    Identifier = statement[offset].Value,
                    OptionalType = innerTerm
                };
            }
        }
        
        if (statement.Count - offset > 3 && statement[offset].Type == TokenType.NamespaceIdentifier &&
            statement[offset + 1].Type == TokenType.Dot &&
            statement[offset + 2].Type == TokenType.TypeIdentifier &&
            statement[offset + 3].Type != TokenType.Langle)
        {
            consumed += 3;
            return new TypeTermSyntax()
            {
                IsBare = isBare,
                IsTypeOf = isTypeOf,
                NamespaceIdentifier = statement[offset].Value,
                Identifier = statement[offset + 2].Value
            };
        }
        
        if (statement.Count - offset > 4 && statement[offset].Type == TokenType.NamespaceIdentifier &&
            statement[offset + 1].Type == TokenType.Dot &&
            statement[offset + 2].Type == TokenType.TypeIdentifier &&
            statement[offset + 3].Type == TokenType.Langle)
        {
            consumed += 4;
            var innerTerm = ParseTypeTerm(statement, offset + consumed, out var consumed2);
            consumed += consumed2;
            if (statement[offset + consumed].Type == TokenType.Rangle)
            {
                consumed++;
                return new TypeTermSyntax()
                {
                    IsBare = isBare,
                    IsTypeOf = isTypeOf,
                    NamespaceIdentifier = statement[offset].Value,
                    Identifier = statement[offset + 2].Value,
                    OptionalType = innerTerm
                };
            }
        }

        if (statement.Count - offset > 1 && statement[offset].Type == TokenType.TypeIdentifier &&
            statement[offset + 1].Type != TokenType.Langle)
        {
            consumed += 1;
            return new TypeTermSyntax()
            {
                IsBare = isBare,
                IsTypeOf = isTypeOf,
                Identifier = statement[offset].Value
            };
        }
        
        if (statement.Count - offset > 3 && statement[offset].Type == TokenType.TypeIdentifier &&
            statement[offset + 1].Type == TokenType.Langle)
        {
            consumed += 2;
            var innerTerm = ParseTypeTerm(statement, offset + consumed, out var consumed2);
            consumed += consumed2;
            if (statement[offset + consumed].Type == TokenType.Rangle)
            {
                consumed++;
                return new TypeTermSyntax()
                {
                    IsBare = isBare,
                    IsTypeOf = isTypeOf,
                    Identifier = statement[offset].Value,
                    OptionalType = innerTerm
                };
            }
        }
        return null;
    }

    private IReadOnlyList<Token> NextStatement()
    {
        List<Token> tokens = new List<Token>();
        
        var token = _lexer.Lex();
        if (token.Type != TokenType.Spaces &&
            token.Type != TokenType.LineComment &&
            token.Type != TokenType.MultilineComment)
        {
            tokens.Add(token);
        }
        while (token.Type != TokenType.EOF && 
               token.Type != TokenType.EOL)
        {
            token = _lexer.Lex();
            if (token.Type != TokenType.Spaces &&
                token.Type != TokenType.LineComment &&
                token.Type != TokenType.MultilineComment)
            {
                tokens.Add(token);
            }
        }

        return tokens;
    }
}
