// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.Services.Langpack;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.langpack;

namespace Ferrite.Services.Handlers.LangPackMethods;

public sealed class GetDifferenceHandler : LangPackHandlerBase
{
    public GetDifferenceHandler(IUnitOfWork unitOfWork, ILangPackRepository langPackRepository,
        IAppInfoRepository appInfoRepository)
        : base(unitOfWork, langPackRepository, appInfoRepository)
    {
    }

    [TLFunction(Constructors.baseLayer_LangpackGetDifference)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
        {
            var request = new LangpackGetDifference(q.AsSpan());
            string? langPack = ResolveLangPack(authKeyId, request.LangPack);
            string langCode = Encoding.UTF8.GetString(request.LangCode);
            int fromVersion = request.FromVersion;
            TLLangPackDifference? difference = langPack == null
                ? null
                : await GetDifferenceAsync(langPack, langCode, fromVersion);
            return LangPackResultBuilder.BuildDifference(difference, langCode, fromVersion);
        }
}
