// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using System.Text.Json;
using Ferrite.TL;
using Ferrite.TL.baseLayer;

namespace Ferrite.Data.Repositories;

public class LangPackRepository : ILangPackRepository
{
    private readonly IKVStore _store;
    private readonly IKVStore _storeStrings;
    private bool _loadingFromDisk;
    private readonly Task _loadFromDisk;

    public LangPackRepository(IKVStore store, IKVStore storeStrings)
    {
        _store = store;
        _store.SetSchema(new TableDefinition("ferrite", "lang_packs_tl1",
            new KeyDefinition("pk",
                new DataColumn { Name = "lang_pack", Type = DataType.String },
                new DataColumn { Name = "lang_code", Type = DataType.String })));
        _storeStrings = storeStrings;
        _storeStrings.SetSchema(new TableDefinition("ferrite", "lang_pack_strings_tl1",
            new KeyDefinition("pk",
                new DataColumn { Name = "lang_pack", Type = DataType.String },
                new DataColumn { Name = "lang_code", Type = DataType.String })));
        _loadFromDisk = LoadFromDisk();
    }

    private async Task LoadFromDisk()
    {
        if (_loadingFromDisk) return;
        _loadingFromDisk = true;
        try
        {
            if (_store.Iterate("android").FirstOrDefault() != null) return;
            string[] langPacks = ["android", "ios", "tdesktop", "macos", "android_x"];
            var options = new JsonSerializerOptions { IncludeFields = true };
            foreach (string langPack in langPacks)
            {
                await using Stream languagesStream = File.OpenRead(
                    $"LangData/{langPack}-languages.json");
                LanguageJson[] languages = await JsonSerializer.DeserializeAsync<LanguageJson[]>(
                    languagesStream, options) ?? [];
                foreach (LanguageJson language in languages)
                {
                    using TLLangPackLanguage languageRow = BuildLanguage(language);
                    SaveLanguage(langPack, languageRow);
                    await using Stream differenceStream = File.OpenRead(
                        $"LangData/{langPack}-{language.LangCode}.json");
                    DifferenceJson? difference =
                        await JsonSerializer.DeserializeAsync<DifferenceJson>(
                            differenceStream, options);
                    if (difference != null)
                    {
                        using TLLangPackDifference differenceRow = BuildDifference(difference);
                        SaveLangPackDifference(langPack, differenceRow);
                    }
                }
            }
        }
        finally
        {
            _loadingFromDisk = false;
        }
    }

    public bool SaveLanguage(string langPack, TLLangPackLanguage language)
    {
        string langCode = Encoding.UTF8.GetString(language.AsLangPackLanguage().LangCode);
        return _store.Put(language.AsSpan().ToArray(), langPack, langCode);
    }

    public bool SaveLangPackDifference(string langPack, TLLangPackDifference difference)
    {
        string langCode = Encoding.UTF8.GetString(
            difference.AsLangPackDifference().LangCode);
        return _storeStrings.Put(difference.AsSpan().ToArray(), langPack, langCode);
    }

    public async ValueTask<List<TLLangPackLanguage>> GetLanguagesAsync(string? langPack)
    {
        await AwaitLoad();
        var result = new List<TLLangPackLanguage>();
        foreach (byte[] bytes in _store.Iterate(langPack))
        {
            result.Add(ReadLanguage(bytes));
        }
        return result;
    }

    public async ValueTask<TLLangPackLanguage?> GetLanguageAsync(string langPack,
        string langCode)
    {
        await AwaitLoad();
        byte[]? bytes = _store.Get(langPack, langCode);
        return bytes == null ? null : ReadLanguage(bytes);
    }

    public async ValueTask<TLLangPackDifference?> GetLangPackAsync(string langPack,
        string langCode)
    {
        await AwaitLoad();
        return GetDifferenceInternal(langPack, langCode, 0);
    }

    public async ValueTask<TLLangPackDifference?> GetDifferenceAsync(string langPack,
        string langCode, int fromVersion)
    {
        await AwaitLoad();
        return GetDifferenceInternal(langPack, langCode, fromVersion);
    }

    public async ValueTask<List<TLLangPackString>> GetStringsAsync(string langPack,
        string langCode, ICollection<string> keys)
    {
        await AwaitLoad();
        var result = new List<TLLangPackString>();
        using TLLangPackDifference? difference =
            GetDifferenceInternal(langPack, langCode, 0);
        if (difference == null) return result;
        var strings = difference.Value.AsLangPackDifference().Strings;
        for (int i = 0; i < strings.Count; i++)
        {
            Span<byte> element = strings.ReadTLObject();
            if (!keys.Contains(ReadStringKey(element))) continue;
            byte[] copy = element.ToArray();
            result.Add((TLLangPackString)new TLBytes(copy, 0, copy.Length));
        }
        return result;
    }

    private TLLangPackDifference? GetDifferenceInternal(string langPack,
        string langCode, int fromVersion)
    {
        int currentVersion = fromVersion;
        var strings = new Dictionary<string, byte[]>();
        foreach (byte[] bytes in _storeStrings.Iterate(langPack, langCode))
        {
            TLLangPackDifference stored = ReadDifference(bytes);
            var difference = stored.AsLangPackDifference();
            if (difference.Version <= currentVersion) continue;
            currentVersion = difference.Version;
            var vector = difference.Strings;
            for (int i = 0; i < vector.Count; i++)
            {
                Span<byte> element = vector.ReadTLObject();
                strings[ReadStringKey(element)] = element.ToArray();
            }
        }
        if (fromVersion == 0 && strings.Count == 0) return null;

        var resultStrings = new Vector();
        foreach (byte[] value in strings.Values) resultStrings.AppendTLObject(value);
        LangPackDifference result = LangPackDifference.Builder()
            .LangCode(Encoding.UTF8.GetBytes(langCode))
            .FromVersion(fromVersion)
            .Version(currentVersion)
            .Strings(resultStrings)
            .Build();
        return result;
    }

    private async ValueTask AwaitLoad()
    {
        if (_loadingFromDisk) await _loadFromDisk;
    }

    private static TLLangPackLanguage ReadLanguage(byte[] bytes)
    {
        var value = new TLBytes(bytes, 0, bytes.Length);
        if (value.Constructor != Constructors.baseLayer_LangPackLanguage)
            throw new InvalidDataException("Langpack language codec/version mismatch.");
        return (TLLangPackLanguage)value;
    }

    private static TLLangPackDifference ReadDifference(byte[] bytes)
    {
        var value = new TLBytes(bytes, 0, bytes.Length);
        if (value.Constructor != Constructors.baseLayer_LangPackDifference)
            throw new InvalidDataException("Langpack difference codec/version mismatch.");
        return (TLLangPackDifference)value;
    }

    private static string ReadStringKey(Span<byte> bytes)
    {
        var view = (LangPackStringView)bytes;
        ReadOnlySpan<byte> key = view.Type switch
        {
            TLLangPackString.LangPackStringType.LangPackString =>
                view.AsLangPackString().Key,
            TLLangPackString.LangPackStringType.LangPackStringPluralized =>
                view.AsLangPackStringPluralized().Key,
            TLLangPackString.LangPackStringType.LangPackStringDeleted =>
                view.AsLangPackStringDeleted().Key,
            _ => throw new InvalidDataException("Invalid langpack string row."),
        };
        return Encoding.UTF8.GetString(key);
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
