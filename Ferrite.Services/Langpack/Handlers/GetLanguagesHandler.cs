// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.Services.Langpack;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.langpack;

namespace Ferrite.Services.Handlers.LangPackMethods;

public sealed class GetLanguagesHandler : LangPackHandlerBase
{
    public GetLanguagesHandler(IUnitOfWork unitOfWork, ILangPackRepository langPackRepository,
        IAppInfoRepository appInfoRepository)
        : base(unitOfWork, langPackRepository, appInfoRepository)
    {
    }

    [TLFunction(Constructors.baseLayer_GetLanguages)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
        {
            var request = new GetLanguages(q.AsSpan());
            return await AnswerAsync(ResolveLangPack(authKeyId, request.LangPack));
        }

    [TLFunction(Constructors.layer67_LangpackGetLanguages)]
    public async Task<TLBytes> HandleLayer67(long authKeyId, TLBytes q)
        => await AnswerAsync(ResolveLangPack(authKeyId, ReadOnlySpan<byte>.Empty));

    private async Task<TLBytes> AnswerAsync(string? langPack)
        {
            ICollection<TLLangPackLanguage> languages = await GetLanguagesAsync(langPack);
            return LangPackResultBuilder.BuildLanguages(languages);
        }
}
