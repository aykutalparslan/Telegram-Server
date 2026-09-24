// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using System.Text.Json;
using Ferrite.Data.Models;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.Utils;

namespace Ferrite.Services.Langpack;

public sealed class LangPackSeed
{
    private const string DataDirectory = "LangData";
    private static readonly string[] LangPacks =
        ["android", "ios", "tdesktop", "macos", "android_x"];
    private static readonly JsonSerializerOptions JsonOptions = new() { IncludeFields = true };
    private readonly ILangPackRepository _langPacks;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger _log;

    public LangPackSeed(ILangPackRepository langPacks, IUnitOfWork unitOfWork, ILogger log)
    {
        _langPacks = langPacks;
        _unitOfWork = unitOfWork;
        _log = log;
    }

    public async ValueTask<int> RunAsync()
    {
        int seeded = 0;
        if (!_langPacks.HasLanguages(LangPacks[0]))
        {
            foreach (string langPack in LangPacks)
            {
                seeded += await SeedAsync(langPack);
            }
        }

        _log.Information($"Lang pack seed: seeded={seeded}");
        return seeded;
    }

    private async ValueTask<int> SeedAsync(string langPack)
    {
        LanguageJson[] languages =
            await ReadAsync<LanguageJson[]>($"{langPack}-languages.json") ?? [];
        foreach (LanguageJson language in languages)
        {
            using (TLLangPackLanguage row = BuildLanguage(language))
            {
                Require(_langPacks.SaveLanguage(langPack, row), langPack, language.LangCode);
            }
            await CommitAsync();

            DifferenceJson? difference =
                await ReadAsync<DifferenceJson>($"{langPack}-{language.LangCode}.json");
            if (difference == null)
            {
                continue;
            }
            using (TLLangPackDifference row = BuildDifference(difference))
            {
                Require(_langPacks.SaveLangPackDifference(langPack, row), langPack,
                    language.LangCode);
            }
            await CommitAsync();
        }
        return languages.Length;
    }

    private async ValueTask CommitAsync()
    {
        if (!await _unitOfWork.SaveAsync())
        {
            throw new InvalidOperationException("Unable to commit the lang pack seed.");
        }
    }

    private static void Require(bool saved, string langPack, string langCode)
    {
        if (!saved)
        {
            throw new InvalidOperationException(
                $"Unable to seed lang pack {langPack}/{langCode}.");
        }
    }

    private static async ValueTask<T?> ReadAsync<T>(string name)
    {
        await using Stream stream = File.OpenRead(Path.Combine(DataDirectory, name));
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions);
    }

    private static TLLangPackLanguage BuildLanguage(LanguageJson value)
    {
        var builder = LangPackLanguage.Builder()
            .Official(value.Official)
            .Rtl(value.Rtl)
            .Beta(value.Beta)
            .Name(Utf8(value.Name))
            .NativeName(Utf8(value.NativeName))
            .LangCode(Utf8(value.LangCode))
            .PluralCode(Utf8(value.PluralCode))
            .StringsCount(value.StringsCount)
            .TranslatedCount(value.TranslatedCount)
            .TranslationsUrl(Utf8(value.TranslationsUrl));
        if (!string.IsNullOrEmpty(value.BaseLangCode))
            builder.BaseLangCode(Utf8(value.BaseLangCode));
        LangPackLanguage row = builder.Build();
        return row;
    }

    private static TLLangPackDifference BuildDifference(DifferenceJson value)
    {
        var strings = new Vector();
        foreach (StringJson stringValue in value.Strings)
        {
            using TLLangPackString stringRow = BuildString(stringValue);
            strings.AppendTLObject(stringRow.AsSpan());
        }
        LangPackDifference row = LangPackDifference.Builder()
            .LangCode(Utf8(value.LangCode))
            .FromVersion(value.FromVersion)
            .Version(value.Version)
            .Strings(strings)
            .Build();
        return row;
    }

    private static TLLangPackString BuildString(StringJson value)
    {
        if (value.StringType == LangPackStringType.Deleted)
        {
            LangPackStringDeleted row = LangPackStringDeleted.Builder()
                .Key(Utf8(value.Key)).Build();
            return row;
        }
        if (value.StringType == LangPackStringType.Pluralized)
        {
            var builder = LangPackStringPluralized.Builder()
                .Key(Utf8(value.Key)).OtherValue(Utf8(value.OtherValue));
            if (!string.IsNullOrEmpty(value.ZeroValue)) builder.ZeroValue(Utf8(value.ZeroValue));
            if (!string.IsNullOrEmpty(value.OneValue)) builder.OneValue(Utf8(value.OneValue));
            if (!string.IsNullOrEmpty(value.TwoValue)) builder.TwoValue(Utf8(value.TwoValue));
            if (!string.IsNullOrEmpty(value.FewValue)) builder.FewValue(Utf8(value.FewValue));
            if (!string.IsNullOrEmpty(value.ManyValue)) builder.ManyValue(Utf8(value.ManyValue));
            LangPackStringPluralized row = builder.Build();
            return row;
        }
        LangPackString plain = LangPackString.Builder()
            .Key(Utf8(value.Key)).Value(Utf8(value.Value)).Build();
        return plain;
    }

    private static byte[] Utf8(string? value) =>
        Encoding.UTF8.GetBytes(value ?? string.Empty);

    private sealed class LanguageJson
    {
        public bool Official { get; set; }
        public bool Rtl { get; set; }
        public bool Beta { get; set; }
        public string Name { get; set; } = "";
        public string NativeName { get; set; } = "";
        public string LangCode { get; set; } = "";
        public string? BaseLangCode { get; set; }
        public string PluralCode { get; set; } = "";
        public int StringsCount { get; set; }
        public int TranslatedCount { get; set; }
        public string TranslationsUrl { get; set; } = "";
    }

    private sealed class DifferenceJson
    {
        public string LangCode { get; set; } = "";
        public int FromVersion { get; set; }
        public int Version { get; set; }
        public List<StringJson> Strings { get; set; } = [];
    }

    private sealed class StringJson
    {
        public LangPackStringType StringType { get; set; }
        public string Key { get; set; } = "";
        public string? Value { get; set; }
        public string? ZeroValue { get; set; }
        public string? OneValue { get; set; }
        public string? TwoValue { get; set; }
        public string? FewValue { get; set; }
        public string? ManyValue { get; set; }
        public string? OtherValue { get; set; }
    }
}
