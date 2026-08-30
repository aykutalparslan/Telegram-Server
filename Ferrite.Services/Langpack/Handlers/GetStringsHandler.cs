// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.Services.Langpack;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.langpack;

namespace Ferrite.Services.Handlers.LangPackMethods;

public sealed class GetStringsHandler : LangPackHandlerBase
{
    public GetStringsHandler(IUnitOfWork unitOfWork, ILangPackRepository langPackRepository,
        IAppInfoRepository appInfoRepository)
        : base(unitOfWork, langPackRepository, appInfoRepository)
    {
    }

    [TLFunction(Constructors.layer67_LangpackGetStrings)]
    public Task<TLBytes> HandleLayer67(long authKeyId, TLBytes q)
    {
        var request = new TL.layer67.langpack.LangpackGetStrings(q.AsSpan());
        return AnswerAsync(ResolveLangPack(authKeyId, ReadOnlySpan<byte>.Empty),
            Encoding.UTF8.GetString(request.LangCode), ReadKeys(request.Keys));
    }

    [TLFunction(Constructors.baseLayer_GetStrings)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
        {
            var request = new GetStrings(q.AsSpan());
            string? langPack = ResolveLangPack(authKeyId, request.LangPack);
            string langCode = Encoding.UTF8.GetString(request.LangCode);
            List<string> keys = ReadKeys(request.Keys);
            return await AnswerAsync(langPack, langCode, keys);
        }

    private async Task<TLBytes> AnswerAsync(string? langPack, string langCode,
        List<string> keys)
    {
        ICollection<TLLangPackString> strings = langPack == null
            ? []
            : await GetStringsAsync(langPack, langCode, keys);
        return LangPackResultBuilder.BuildStrings(strings);
    }
}
