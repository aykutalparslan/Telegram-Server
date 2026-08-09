// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;

namespace Ferrite.Services.Handlers.LangPackMethods;

public abstract class LangPackHandlerBase
{
    private readonly ILangPackRepository _langPackRepository;

    protected readonly IUnitOfWork UnitOfWork;

    protected LangPackHandlerBase(IUnitOfWork unitOfWork, ILangPackRepository langPackRepository)
    {
        _langPackRepository = langPackRepository;

        UnitOfWork = unitOfWork;
    }

    protected Task<TLLangPackDifference?> GetDifferenceAsync(string langPack,
        string langCode, int fromVersion) => _langPackRepository
        .GetDifferenceAsync(langPack, langCode, fromVersion).AsTask();

    protected Task<TLLangPackDifference?> GetLangPackAsync(string langPack,
        string langCode) => _langPackRepository
        .GetLangPackAsync(langPack, langCode).AsTask();

    protected Task<TLLangPackLanguage?> GetLanguageAsync(string langPack,
        string langCode) => _langPackRepository
        .GetLanguageAsync(langPack, langCode).AsTask();

    protected async Task<ICollection<TLLangPackLanguage>> GetLanguagesAsync(
        string langPack) => await _langPackRepository
        .GetLanguagesAsync(langPack);

    protected async Task<ICollection<TLLangPackString>> GetStringsAsync(
        string langPack, string langCode, ICollection<string> keys) =>
        await _langPackRepository.GetStringsAsync(langPack, langCode, keys);

    protected static List<string> ReadKeys(VectorOfString vector)
    {
        var keys = new List<string>(vector.Count);
        for (int i = 0; i < vector.Count; i++)
        {
            keys.Add(Encoding.UTF8.GetString(vector.ReadTLBytes()));
        }

        return keys;
    }
}
