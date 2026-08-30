// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.TLParser;

public class TextWindow
{
    public const char InvalidChar = Char.MaxValue;
    private char[] _text;
    private int _offset;
    private int _line;
    private int _column;
    public int CurrentLine => _line;
    public int CurrentColumn => _column;

    public TextWindow(char[] text)
    {
        _text = text;
    }

    public void Advance(int count = 1)
    {
        _column += count;
        _offset += count;
    }
    
    public void AdvancePastNewLine()
    {
        _line++;
        _column = 0;
        Advance(NewLineWidth());
    }
    
    public char Peek(int delta = 0)
    {
        return _offset + delta >= _text.Length ? InvalidChar : 
            _text[_offset + delta];
    }

    public bool IsExactMatch(string value, int delta = 0)
    {
        if (_offset + delta + value.Length > _text.Length)
        {
            return false;
        }

        for (int i = 0; i < value.Length; i++)
        {
            if (_text[_offset + delta + i] != value[i])
            {
                return false;
            }
        }

        return true;
    }
    
    public char Next()
    {
        char c = Peek();
        if (c != InvalidChar)
        {
            Advance();
        }
        return c;
    }

    public int NewLineWidth()
    {
        return Peek() == '\r' && Peek(1) == '\n' ? 2 : 1;
    }

    public string StringSlice(int length)
    {
        if (_offset + length >= _text.Length)
        {
            return new string(_text.AsSpan(_offset).ToArray());
        }
        return new string(_text.AsSpan(_offset, length).ToArray());
    }
    
    public void Reset(int position = 0)
    {
        if (position < 0)
        {
            _offset = 0;
        }
        else if(position >= _text.Length)
        {
            _offset = _text.Length;
        }
        else
        {
            _offset = position;
        }
    }
}