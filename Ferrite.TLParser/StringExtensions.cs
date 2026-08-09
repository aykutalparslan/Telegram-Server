// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Globalization;

namespace Ferrite.TLParser;

public static class StringExtensions
{
    public static string ToPascalCase(this string str){
        if (str.Contains("."))
        {
            return str;
        }
        if (!str.Contains("_"))
        {
            return str.FirstLetterToUpperCase();
        }
        str = str.ToLower().Replace("_", " ");
        TextInfo info = CultureInfo.InvariantCulture.TextInfo;
        return info.ToTitleCase(str).Replace(" ", string.Empty);
    }
    public static string ToCamelCase(this string str)
    {
        var chars = str.ToPascalCase().ToCharArray();
        chars[0] = Char.ToLower(chars[0]);
        return new string(chars);
    }
    public static string FirstLetterToUpperCase(this string str)
    {
        var chars = str.ToCharArray();
        chars[0] = Char.ToUpper(chars[0]);
        return new string(chars);
    }
}