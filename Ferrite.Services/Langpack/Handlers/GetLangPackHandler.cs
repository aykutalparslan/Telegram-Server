// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.Services.Langpack;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.langpack;

namespace Ferrite.Services.Handlers.LangPackMethods;

public sealed class GetLangPackHandler : LangPackHandlerBase
{
    public GetLangPackHandler(IUnitOfWork unitOfWork, ILangPackRepository langPackRepository,
        IAppInfoRepository appInfoRepository)
        : base(unitOfWork, langPackRepository, appInfoRepository)
    {
    }

    [TLFunction(Constructors.layer67_LangpackGetLangPack)]
    public Task<TLBytes> HandleLayer67(long authKeyId, TLBytes q)
    {
        var request = new TL.layer67.langpack.LangpackGetLangPack(q.AsSpan());
        return AnswerAsync(ResolveLangPack(authKeyId, ReadOnlySpan<byte>.Empty),
            Encoding.UTF8.GetString(request.LangCode));
    }

    [TLFunction(Constructors.baseLayer_GetLangPack)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
        {
            var request = new GetLangPack(q.AsSpan());
            string? langPack = ResolveLangPack(authKeyId, request.LangPack);
            string langCode = Encoding.UTF8.GetString(request.LangCode);
            return await AnswerAsync(langPack, langCode);
        }

    private async Task<TLBytes> AnswerAsync(string? langPack, string langCode)
    {
        TLLangPackDifference? difference = langPack == null
            ? null
            : await GetLangPackAsync(langPack, langCode);
        return LangPackResultBuilder.BuildDifference(difference, langCode, 0);
    }
}
