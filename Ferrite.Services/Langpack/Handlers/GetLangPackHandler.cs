// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.langpack;

namespace Ferrite.Services.Handlers.LangPackMethods;

public sealed class GetLangPackHandler : LangPackHandlerBase
{
    public GetLangPackHandler(IUnitOfWork unitOfWork, ILangPackRepository langPackRepository)
        : base(unitOfWork, langPackRepository)
    {
    }

    [TLFunction(Constructors.baseLayer_GetLangPack)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
        {
            var request = new GetLangPack(q.AsSpan());
            string langPack = Encoding.UTF8.GetString(request.LangPack);
            string langCode = Encoding.UTF8.GetString(request.LangCode);
            TLLangPackDifference? difference = await GetLangPackAsync(langPack, langCode);
            return LangPackResultBuilder.BuildDifference(difference, langCode, 0);
        }
}
