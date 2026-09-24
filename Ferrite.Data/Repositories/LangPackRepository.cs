// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL;
using Ferrite.TL.baseLayer;

namespace Ferrite.Data.Repositories;

public class LangPackRepository : ILangPackRepository
{
    private readonly IKVStore _store;
    private readonly IKVStore _storeStrings;

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

    public bool HasLanguages(string langPack) => _store.Iterate(langPack).Any();

    public ValueTask<List<TLLangPackLanguage>> GetLanguagesAsync(string? langPack)
    {
        var result = new List<TLLangPackLanguage>();
        IEnumerable<byte[]> found = string.IsNullOrEmpty(langPack)
            ? _store.Iterate()
            : _store.Iterate(langPack);
        foreach (byte[] bytes in found)
        {
            result.Add(ReadLanguage(bytes));
        }
        return new(result);
    }

    public ValueTask<TLLangPackLanguage?> GetLanguageAsync(string langPack,
        string langCode)
    {
        byte[]? bytes = _store.Get(langPack, langCode);
        return new(bytes == null ? null : ReadLanguage(bytes));
    }

    public ValueTask<TLLangPackDifference?> GetLangPackAsync(string langPack,
        string langCode) =>
        new(GetDifferenceInternal(langPack, langCode, 0));

    public ValueTask<TLLangPackDifference?> GetDifferenceAsync(string langPack,
        string langCode, int fromVersion) =>
        new(GetDifferenceInternal(langPack, langCode, fromVersion));

    public ValueTask<List<TLLangPackString>> GetStringsAsync(string langPack,
        string langCode, ICollection<string> keys)
    {
        var result = new List<TLLangPackString>();
        using TLLangPackDifference? difference =
            GetDifferenceInternal(langPack, langCode, 0);
        if (difference == null) return new(result);
        var strings = difference.Value.AsLangPackDifference().Strings;
        for (int i = 0; i < strings.Count; i++)
        {
            Span<byte> element = strings.ReadTLObject();
            if (!keys.Contains(ReadStringKey(element))) continue;
            byte[] copy = element.ToArray();
            result.Add((TLLangPackString)new TLBytes(copy, 0, copy.Length));
        }
        return new(result);
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
}
