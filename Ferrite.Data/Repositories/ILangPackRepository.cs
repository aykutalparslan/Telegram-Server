// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Data.Repositories;

using Ferrite.TL.baseLayer;

public interface ILangPackRepository
{
    public bool SaveLanguage(string langPack, TLLangPackLanguage language);
    public bool SaveLangPackDifference(string langPack, TLLangPackDifference difference);
    public ValueTask<List<TLLangPackLanguage>> GetLanguagesAsync(string? langPack);
    public ValueTask<TLLangPackLanguage?> GetLanguageAsync(string langPack, string langCode);
    public ValueTask<TLLangPackDifference?> GetLangPackAsync(string langPack, string langCode);
    public ValueTask<TLLangPackDifference?> GetDifferenceAsync(string langPack, string langCode, int fromVersion);
    public ValueTask<List<TLLangPackString>> GetStringsAsync(string langPack, string langCode,
        ICollection<string> keys);
}
