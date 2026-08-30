// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Ferrite.TLParser
{
    public class Lexer
    {
        private TextWindow _textWindow;
        private TokenType previousTokenType = TokenType.None;
        public Lexer(string source)
        {
            _textWindow = new TextWindow(source.ToCharArray());
        }
        public Token Lex()
        {
            switch (_textWindow.Peek())
            {
                case '\r':
                    _textWindow.AdvancePastNewLine();
                    previousTokenType = TokenType.EOL;
                    return new Token()
                    {
                        Type = TokenType.EOL,
                        Line = _textWindow.CurrentLine,
                        Column = _textWindow.CurrentColumn-1,
                    };
                case '\n':
                    _textWindow.AdvancePastNewLine();
                    previousTokenType = TokenType.EOL;
                    return new Token()
                    {
                        Type = TokenType.EOL,
                        Line = _textWindow.CurrentLine,
                        Column = _textWindow.CurrentColumn-1,
                    };
                case '/':
                    if (ScanComment(out Token token)) return token;
                    _textWindow.Advance();
                    previousTokenType = TokenType.Slash;
                    return new Token()
                    {
                        Type = TokenType.Slash,
                        Line = _textWindow.CurrentLine,
                        Column = _textWindow.CurrentColumn-1,
                    };
                case '-':
                    if (ScanFunctions(out token)) return token;
                    if (ScanTypes(out token)) return token;
                    _textWindow.Advance(1);
                    previousTokenType = TokenType.Minus;
                    return new Token()
                    {
                        Type = TokenType.Minus,
                        Line = _textWindow.CurrentLine,
                        Column = _textWindow.CurrentColumn-1,
                    };
                case ' ':
                    int count = 0;
                    while (_textWindow.Peek(++count) == ' '){}
                    _textWindow.Advance(count);
                    previousTokenType = TokenType.Spaces;
                    return new Token()
                    {
                        Type = TokenType.Spaces,
                        Line = _textWindow.CurrentLine,
                        Column = _textWindow.CurrentColumn-count,
                    };
                case '[':
                    _textWindow.Advance(1);
                    previousTokenType = TokenType.OpenBracket;
                    return new Token()
                    {
                        Type = TokenType.OpenBracket,
                        Line = _textWindow.CurrentLine,
                        Column = _textWindow.CurrentColumn-1,
                    };
                case ']':
                    _textWindow.Advance(1);
                    previousTokenType = TokenType.CloseBracket;
                    return new Token()
                    {
                        Type = TokenType.CloseBracket,
                        Line = _textWindow.CurrentLine,
                        Column = _textWindow.CurrentColumn-1,
                    };
                case '{':
                    _textWindow.Advance(1);
                    previousTokenType = TokenType.OpenBrace;
                    return new Token()
                    {
                        Type = TokenType.OpenBrace,
                        Line = _textWindow.CurrentLine,
                        Column = _textWindow.CurrentColumn-1,
                    };
                case '}':
                    _textWindow.Advance(1);
                    previousTokenType = TokenType.CloseBrace;
                    return new Token()
                    {
                        Type = TokenType.CloseBrace,
                        Line = _textWindow.CurrentLine,
                        Column = _textWindow.CurrentColumn-1,
                    };
                case '(':
                    _textWindow.Advance(1);
                    previousTokenType = TokenType.OpenParen;
                    return new Token()
                    {
                        Type = TokenType.OpenParen,
                        Line = _textWindow.CurrentLine,
                        Column = _textWindow.CurrentColumn-1,
                    };
                case ')':
                    _textWindow.Advance(1);
                    previousTokenType = TokenType.CloseParen;
                    return new Token()
                    {
                        Type = TokenType.CloseParen,
                        Line = _textWindow.CurrentLine,
                        Column = _textWindow.CurrentColumn-1,
                    };
                case '=':
                    _textWindow.Advance(1);
                    previousTokenType = TokenType.Equal;
                    return new Token()
                    {
                        Type = TokenType.Equal,
                        Line = _textWindow.CurrentLine,
                        Column = _textWindow.CurrentColumn-1,
                    };
                case '.':
                    _textWindow.Advance(1);
                    previousTokenType = TokenType.Dot;
                    return new Token()
                    {
                        Type = TokenType.Dot,
                        Line = _textWindow.CurrentLine,
                        Column = _textWindow.CurrentColumn-1,
                    };
                case ':':
                    _textWindow.Advance(1);
                    previousTokenType = TokenType.Colon;
                    return new Token()
                    {
                        Type = TokenType.Colon,
                        Line = _textWindow.CurrentLine,
                        Column = _textWindow.CurrentColumn,
                    };
                case ';':
                    _textWindow.Advance(1);
                    previousTokenType = TokenType.Semicolon;
                    return new Token()
                    {
                        Type = TokenType.Semicolon,
                        Line = _textWindow.CurrentLine,
                        Column = _textWindow.CurrentColumn-1,
                    };
                case '?':
                    _textWindow.Advance(1);
                    previousTokenType = TokenType.QuestionMark;
                    return new Token()
                    {
                        Type = TokenType.QuestionMark,
                        Line = _textWindow.CurrentLine,
                        Column = _textWindow.CurrentColumn,
                    };
                case '!':
                    _textWindow.Advance(1);
                    previousTokenType = TokenType.ExclamationMark;
                    return new Token()
                    {
                        Type = TokenType.ExclamationMark,
                        Line = _textWindow.CurrentLine,
                        Column = _textWindow.CurrentColumn-1,
                    };
                case '#':
                    _textWindow.Advance(1);
                    previousTokenType = TokenType.Hash;
                    return new Token()
                    {
                        Type = TokenType.Hash,
                        Line = _textWindow.CurrentLine,
                        Column = _textWindow.CurrentColumn-1,
                    };
                case '%':
                    _textWindow.Advance(1);
                    previousTokenType = TokenType.Percent;
                    return new Token()
                    {
                        Type = TokenType.Percent,
                        Line = _textWindow.CurrentLine,
                        Column = _textWindow.CurrentColumn-1,
                    };
                case '<':
                    _textWindow.Advance(1);
                    previousTokenType = TokenType.Langle;
                    return new Token()
                    {
                        Type = TokenType.Langle,
                        Line = _textWindow.CurrentLine,
                        Column = _textWindow.CurrentColumn-1,
                    };
                case '>':
                    _textWindow.Advance(1);
                    previousTokenType = TokenType.Rangle;
                    return new Token()
                    {
                        Type = TokenType.Rangle,
                        Line = _textWindow.CurrentLine,
                        Column = _textWindow.CurrentColumn-1,
                    };
                case '*':
                    _textWindow.Advance(1);
                    previousTokenType = TokenType.Multiply;
                    return new Token()
                    {
                        Type = TokenType.Multiply,
                        Line = _textWindow.CurrentLine,
                        Column = _textWindow.CurrentColumn-1,
                    };
                case '`':
                    _textWindow.Advance(1);
                    previousTokenType = TokenType.BackTick;
                    return new Token()
                    {
                        Type = TokenType.BackTick,
                        Line = _textWindow.CurrentLine,
                        Column = _textWindow.CurrentColumn-1,
                    };
                case '+':
                    _textWindow.Advance(1);
                    previousTokenType = TokenType.Plus;
                    return new Token()
                    {
                        Type = TokenType.Plus,
                        Line = _textWindow.CurrentLine,
                        Column = _textWindow.CurrentColumn-1,
                    };
                case 'a':
                case 'b':
                case 'c':
                case 'd':
                case 'e':
                case 'f':
                case 'g':
                case 'h':
                case 'i':
                case 'j':
                case 'k':
                case 'l':
                case 'm':
                case 'n':
                case 'o':
                case 'p':
                case 'q':
                case 'r':
                case 's':
                case 't':
                case 'u':
                case 'v':
                case 'w':
                case 'x':
                case 'y':
                case 'z':
                    if (ScanHexConstant(out token)) return token;
                    if (ScanNamespaceIdentifier(out token)) return token;
                    if (ScanCombinatorIdentifier(out token)) return token;
                    if ((previousTokenType == TokenType.Spaces || 
                        previousTokenType == TokenType.OpenBrace) && 
                        ScanVariableIdentifier(out token)) return token;
                    if (ScanConditionalIdentifier(out token)) return token;
                    if (ScanBareTypeIdentifier(out token)) return token;
                    if (ScanLowercaseIdentifier(out token)) return token;
                    previousTokenType = TokenType.EOF;
                    return new Token()
                    {
                        Type = TokenType.EOF
                    };
                case '0':
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9':
                    if (ScanHexConstant(out token)) return token;
                    if (ScanNumber(out token)) return token;
                    previousTokenType = TokenType.EOF;
                    return new Token()
                    {
                        Type = TokenType.EOF
                    };
                default:
                    if (ScanHexConstant(out token)) return token;
                    if (previousTokenType == TokenType.Spaces && 
                        ScanVariableIdentifier(out token)) return token;
                    if (ScanTypeIdentifier(out token)) return token;
                    previousTokenType = TokenType.EOF;
                    return new Token()
                    {
                        Type = TokenType.EOF
                    };
            }
        }
        private bool ScanComment(out Token token)
        {
            if (_textWindow.Peek(1) == '/')
            {
                int count = 0;
                while (_textWindow.Peek(++count) != TextWindow.InvalidChar)
                {
                    char c = _textWindow.Peek(count + 2);
                    if (c is '\r' or '\n')
                    {
                        break;
                    }
                }

                int column = _textWindow.CurrentColumn;
                _textWindow.Advance(2);
                var val = _textWindow.StringSlice(count);
                _textWindow.Advance(count);
                previousTokenType = TokenType.LineComment;
                token = new Token()
                {
                    Type = TokenType.LineComment,
                    Value = val,
                    Line = _textWindow.CurrentLine,
                    Column = column,
                };
                return true;
            }

            if (_textWindow.Peek(1) == '*')
            {
                int count = 0;
                while (_textWindow.Peek(++count + 1) != TextWindow.InvalidChar)
                {
                    if (_textWindow.Peek(count + 2) == '*' &&
                        _textWindow.Peek(count + 3) == '/')
                    {
                        int column = _textWindow.CurrentColumn;
                        _textWindow.Advance(2);
                        var val = _textWindow.StringSlice(count);
                        _textWindow.Advance(count + 2);
                        previousTokenType = TokenType.MultilineComment;
                        token = new Token()
                        {
                            Type = TokenType.MultilineComment,
                            Value = val,
                            Line = _textWindow.CurrentLine,
                            Column = column,
                        };
                        return true;
                    }
                }
            }

            token = default;
            return false;
        }
        private bool ScanFunctions(out Token token)
        {
            if (_textWindow.Peek(1) == '-' &&
                _textWindow.Peek(2) == '-')
            {
                int pos = 2;
                bool found = false;
                while (_textWindow.Peek(++pos) != TextWindow.InvalidChar)
                {
                    if (char.IsWhiteSpace(_textWindow.Peek(pos)))
                    {
                        continue;
                    } 
                    else if (_textWindow.IsExactMatch("functions", pos))
                    {
                        pos += 8;
                        found = true;
                        continue;
                    }
                    else if (found && _textWindow.IsExactMatch("---", pos))
                    {
                        pos += 3;
                    }
                    else if (found && !char.IsWhiteSpace(_textWindow.Peek(pos)))
                    {
                        found = false;
                    }
                    break;
                }

                if (found)
                {
                    int column = _textWindow.CurrentColumn;
                    _textWindow.Advance(pos);
                    previousTokenType = TokenType.Functions;
                    token = new Token()
                    {
                        Type = TokenType.Functions,
                        Line = _textWindow.CurrentLine,
                        Column = column,
                    };
                    return true;
                }
            }

            token = default;
            return false;
        }
        private bool ScanTypes(out Token token)
        {
            if (_textWindow.Peek(1) == '-' &&
                _textWindow.Peek(2) == '-')
            {
                int pos = 2;
                bool found = false;
                while (_textWindow.Peek(++pos) != TextWindow.InvalidChar)
                {
                    if (char.IsWhiteSpace(_textWindow.Peek(pos)))
                    {
                        continue;
                    } 
                    else if (_textWindow.IsExactMatch("types", pos))
                    {
                        pos += 4;
                        found = true;
                        continue;
                    } 
                    else if (found && _textWindow.IsExactMatch("---", pos))
                    {
                        pos += 3;
                    }
                    else if (found && !char.IsWhiteSpace(_textWindow.Peek(pos)))
                    {
                        found = false;
                    }
                    break;
                }

                if (found)
                {
                    int column = _textWindow.CurrentColumn;
                    _textWindow.Advance(pos);
                    previousTokenType = TokenType.Types;
                    token = new Token()
                    {
                        Type = TokenType.Types,
                        Line = _textWindow.CurrentLine,
                        Column = column,
                    };
                    return true;
                }
            }
            
            token = default;
            return false;
        }
        private bool ScanHexConstant(out Token token)
        {
            int count = 0;
            char c = _textWindow.Peek();
            while (c != TextWindow.InvalidChar)
            {
                switch (c)
                {
                    case 'a':
                    case 'b':
                    case 'c':
                    case 'd':
                    case 'e':
                    case 'f':
                    case 'A':
                    case 'B':
                    case 'C':
                    case 'D':
                    case 'E':
                    case '0':
                    case '1':
                    case '2':
                    case '3':
                    case '4':
                    case '5':
                    case '6':
                    case '7':
                    case '8':
                    case '9':
                        c = _textWindow.Peek(++count);
                        break;
                    default:
                        c = TextWindow.InvalidChar;
                        break;
                }
            }

            if (count is >= 6 and <= 8)
            {
                int column = _textWindow.CurrentColumn;
                var val = _textWindow.StringSlice(count);
                _textWindow.Advance(count);
                previousTokenType = TokenType.HexConstant;
                token = new Token()
                {
                    Type = TokenType.HexConstant,
                    Value = val,
                    Line = _textWindow.CurrentLine,
                    Column = column,
                };
                return true;
            }
            token = default;
            return false;
        }
        private bool ScanNamespaceIdentifier(out Token token)
        {
            int count = 0;
            bool found = false;
            while (_textWindow.Peek(++count) != TextWindow.InvalidChar)
            {
                if(char.IsWhiteSpace(_textWindow.Peek(count)) ||
                   _textWindow.Peek(count) == '<' ||
                   _textWindow.Peek(count) == ':') break;
                if (_textWindow.Peek(count) == '.' &&
                    !char.IsLetter(_textWindow.Peek(count + 1))) break;
                if ((char.IsLetterOrDigit(_textWindow.Peek(count)) ||
                     _textWindow.Peek(count) == '_') &&
                    _textWindow.Peek(count + 1) == '.' &&
                    char.IsLetter(_textWindow.Peek(count + 2)))
                {
                    count++;
                    found = true;
                    break;
                }
            }

            if (found)
            {
                int column = _textWindow.CurrentColumn;
                var val = _textWindow.StringSlice(count);
                _textWindow.Advance(count);
                previousTokenType = TokenType.NamespaceIdentifier;
                token = new Token()
                {
                    Type = TokenType.NamespaceIdentifier,
                    Value = val,
                    Line = _textWindow.CurrentLine,
                    Column = column,
                };
                return true;
            }
            token = default;
            return false;
        }
        private bool ScanCombinatorIdentifier(out Token token)
        {
            int count = 0;
            bool found = false;
            while (_textWindow.Peek(++count) != TextWindow.InvalidChar)
            {
                if(char.IsWhiteSpace(_textWindow.Peek(count))) break;
                if ((char.IsLetterOrDigit(_textWindow.Peek(count)) ||
                     _textWindow.Peek(count) == '_') &&
                    _textWindow.Peek(count + 1) == '#' &&
                    char.IsLetterOrDigit(_textWindow.Peek(count + 2)))
                {
                    count++;
                    found = true;
                    break;
                }
            }

            if (found)
            {
                int column = _textWindow.CurrentColumn;
                var val = _textWindow.StringSlice(count);
                _textWindow.Advance(count);
                previousTokenType = TokenType.CombinatorIdentifier;
                token = new Token()
                {
                    Type = TokenType.CombinatorIdentifier,
                    Value = val,
                    Line = _textWindow.CurrentLine,
                    Column = column,
                };
                return true;
            }
            token = default;
            return false;
        }
        private bool ScanVariableIdentifier(out Token token)
        {
            int count = 0;
            bool found = false;
            while (_textWindow.Peek(++count) != TextWindow.InvalidChar)
            {
                if(char.IsWhiteSpace(_textWindow.Peek(count))) break;
                if (_textWindow.Peek(count) == ':')
                {
                    found = true;
                    break;
                }
                if ((char.IsLetterOrDigit(_textWindow.Peek(count)) ||
                     _textWindow.Peek(count) == '_') &&
                    _textWindow.Peek(count + 1) == ':')
                {
                    count++;
                    found = true;
                    break;
                }
            }

            if (found)
            {
                int column = _textWindow.CurrentColumn;
                var val = _textWindow.StringSlice(count);
                _textWindow.Advance(count);
                previousTokenType = TokenType.VariableIdentifier;
                token = new Token()
                {
                    Type = TokenType.VariableIdentifier,
                    Value = val,
                    Line = _textWindow.CurrentLine,
                    Column = column,
                };
                return true;
            }
            token = default;
            return false;
        }
        private bool ScanConditionalIdentifier(out Token token)
        {
            int count = 0;
            bool found = false;
            while (_textWindow.Peek(++count) != TextWindow.InvalidChar)
            {
                if(char.IsWhiteSpace(_textWindow.Peek(count))) break;
                if ((char.IsLetterOrDigit(_textWindow.Peek(count)) ||
                     _textWindow.Peek(count) == '_') &&
                    _textWindow.Peek(count + 1) == '.' &&
                    ((char.IsDigit(_textWindow.Peek(count + 2)) &&
                    _textWindow.Peek(count + 3) == '?') || (char.IsDigit(_textWindow.Peek(count + 2)) &&
                                                            char.IsDigit(_textWindow.Peek(count + 3)) &&
                                                            _textWindow.Peek(count + 4) == '?')))
                {
                    count++;
                    found = true;
                    break;
                }
            }

            if (found)
            {
                int column = _textWindow.CurrentColumn;
                var val = _textWindow.StringSlice(count);
                _textWindow.Advance(count);
                previousTokenType = TokenType.ConditionalIdentifier;
                token = new Token()
                {
                    Type = TokenType.ConditionalIdentifier,
                    Value = val,
                    Line = _textWindow.CurrentLine,
                    Column = column,
                };
                return true;
            }
            token = default;
            return false;
        }
        private bool ScanTypeIdentifier(out Token token)
        {
            int count = 0;
            bool found = false;
            while (_textWindow.Peek(count) != TextWindow.InvalidChar)
            {
                if(char.IsWhiteSpace(_textWindow.Peek(count))) break;
                if ((char.IsLetterOrDigit(_textWindow.Peek(count)) ||
                     _textWindow.Peek(count) == '_') &&
                    (char.IsWhiteSpace(_textWindow.Peek(count + 1)) ||
                     _textWindow.Peek(count + 1) == ':' ||
                     _textWindow.Peek(count + 1) == ';' ||
                     _textWindow.Peek(count + 1) == '}' ||
                     _textWindow.Peek(count + 1) == '<') ||
                     _textWindow.Peek(count + 1) == '>')
                {
                    count++;
                    found = true;
                    break;
                }

                count++;
            }

            if (found)
            {
                int column = _textWindow.CurrentColumn;
                var val = _textWindow.StringSlice(count);
                _textWindow.Advance(count);
                previousTokenType = TokenType.TypeIdentifier;
                token = new Token()
                {
                    Type = TokenType.TypeIdentifier,
                    Value = val,
                    Line = _textWindow.CurrentLine,
                    Column = column,
                };
                return true;
            }
            token = default;
            return false;
        }
        private bool ScanBareTypeIdentifier(out Token token)
        {
            int count = 0;
            bool found = false;
            while (_textWindow.Peek(++count) != TextWindow.InvalidChar)
            {
                if(char.IsWhiteSpace(_textWindow.Peek(count))) break;
                if ((char.IsLetterOrDigit(_textWindow.Peek(count)) ||
                     _textWindow.Peek(count) == '_') &&
                    (char.IsWhiteSpace(_textWindow.Peek(count + 1)) ||
                     _textWindow.Peek(count + 1) == '<' || 
                     _textWindow.Peek(count + 1) == '>'))
                {
                    count++;
                    found = true;
                    break;
                }
            }

            if (found)
            {
                int column = _textWindow.CurrentColumn;
                var val = _textWindow.StringSlice(count);
                _textWindow.Advance(count);
                previousTokenType = TokenType.BareTypeIdentifier;
                token = new Token()
                {
                    Type = TokenType.BareTypeIdentifier,
                    Value = val,
                    Line = _textWindow.CurrentLine,
                    Column = column,
                };
                return true;
            }
            token = default;
            return false;
        }

        private bool ScanLowercaseIdentifier(out Token token)
        {
            int count = 0;
            while (_textWindow.Peek(++count) != TextWindow.InvalidChar)
            {
                if (char.IsWhiteSpace(_textWindow.Peek(count)) &&
                    _textWindow.Peek(count + 2) != ']') break;
                if (_textWindow.Peek(count) == ';')
                {
                    break;
                }
                if ((char.IsLetterOrDigit(_textWindow.Peek(count)) ||
                     _textWindow.Peek(count) == '_') &&
                    !char.IsLower(_textWindow.Peek(count + 1)))
                {
                    count++;
                    break;
                }
            }
            int column = _textWindow.CurrentColumn;
            var val = _textWindow.StringSlice(count);
            _textWindow.Advance(count);
            previousTokenType = TokenType.LowercaseIdentifier;
            token = new Token()
            {
                Type = TokenType.LowercaseIdentifier,
                Value = val,
                Line = _textWindow.CurrentLine,
                Column = column,
            };
            return true;
        }

        private bool ScanNumber(out Token token)
        {
            int count = 0;
            while (char.IsDigit(_textWindow.Peek(count)))
            {
                count++;
            }

            if (count > 0)
            {
                int column = _textWindow.CurrentColumn;
                var val = _textWindow.StringSlice(count);
                _textWindow.Advance(count);
                previousTokenType = TokenType.Number;
                token = new Token()
                {
                    Type = TokenType.Number,
                    Value = val,
                    Line = _textWindow.CurrentLine,
                    Column = column,
                };
                return true;
            }
            token = default;
            return false;
        }
    }
}