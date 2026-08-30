// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL;
using Ferrite.TL.baseLayer;

namespace Ferrite.Services.Langpack;

internal static class LangPackResultBuilder
{
    public static TLBytes BuildDifference(TLLangPackDifference? difference,
        string fallbackLangCode, int fallbackVersion)
    {
        if (difference != null)
        {
            using TLLangPackDifference row = difference.Value;
            return Copy(row.AsSpan());
        }
        var strings = new Vector();
        using var fallback = LangPackDifference.Builder()
            .LangCode(Encoding.UTF8.GetBytes(fallbackLangCode))
            .FromVersion(fallbackVersion)
            .Version(fallbackVersion)
            .Strings(strings)
            .Build();
        return Copy(fallback.ToReadOnlySpan());
    }

    public static TLBytes BuildStrings(IEnumerable<TLLangPackString> strings)
    {
        var vector = new Vector();
        foreach (TLLangPackString value in strings)
        {
            using (value) vector.AppendTLObject(value.AsSpan());
        }
        return Copy(vector.ToReadOnlySpan());
    }

    public static TLBytes BuildLanguages(IEnumerable<TLLangPackLanguage> languages)
    {
        var vector = new Vector();
        foreach (TLLangPackLanguage value in languages)
        {
            using (value) vector.AppendTLObject(value.AsSpan());
        }
        return Copy(vector.ToReadOnlySpan());
    }

    public static TLBytes BuildLanguage(TLLangPackLanguage? language,
        string fallbackLangCode)
    {
        if (language != null)
        {
            using TLLangPackLanguage row = language.Value;
            return Copy(row.AsSpan());
        }
        byte[] code = Encoding.UTF8.GetBytes(fallbackLangCode);
        using var fallback = LangPackLanguage.Builder()
            .Name(code)
            .NativeName(code)
            .LangCode(code)
            .PluralCode(code)
            .StringsCount(0)
            .TranslatedCount(0)
            .TranslationsUrl([])
            .Build();
        return Copy(fallback.ToReadOnlySpan());
    }

    private static TLBytes Copy(ReadOnlySpan<byte> span)
    {
        byte[] bytes = span.ToArray();
        return new TLBytes(bytes, 0, bytes.Length);
    }
}
