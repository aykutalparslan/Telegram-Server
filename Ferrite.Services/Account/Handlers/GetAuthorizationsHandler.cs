// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Ferrite.Crypto;
using Ferrite.Data.Repositories;
using Ferrite.Services.Gateway;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.account;
using Ferrite.TL.baseLayer.auth;
using Ferrite.TL.baseLayer.dto;
using xxHash;
using Vector = Ferrite.TL.Vector;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class GetAuthorizationsHandler : AccountHandlerBase
{
    private readonly IAppInfoRepository _appInfoRepository;
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly AccountSettingsStore? _accountSettings;

    public GetAuthorizationsHandler(ISearchEngine search, IUpdatesService updates, IRandomGenerator random,
        IUnitOfWork unitOfWork, IAppInfoRepository appInfoRepository, IAuthorizationRepository authorizationRepository, IChatRepository chatRepository, IPrivacyRulesRepository privacyRulesRepository, IUserRepository userRepository, IVerificationGateway verificationGateway,
        AccountSettingsStore? accountSettings = null)
        : base(search, updates, random, unitOfWork, chatRepository, privacyRulesRepository, userRepository, verificationGateway)
    {
        _appInfoRepository = appInfoRepository;
        _authorizationRepository = authorizationRepository;

        _accountSettings = accountSettings;
    }

    [TLFunction(Constructors.baseLayer_GetAuthorizations)]
    public async ValueTask<TLAuthorizations> Handle(long authKeyId, TLBytes q)
        {
            var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
            if(auth == null) return (TLAuthorizations)RpcErrorGenerator.GenerateError(400, "AUTH_KEY_INVALID"u8);
            var authorizations = await _authorizationRepository.GetAuthorizationsAsync(Encoding.UTF8.GetString(auth.Value.AsAuthInfo().Phone));
            List<TLAppInfo> infos = new();
            foreach (var a in authorizations)
            {
                if(!a.AsAuthInfo().LoggedIn) continue;
                var authorization = _appInfoRepository.GetAppInfo(a.AsAuthInfo().AuthKeyId);
                if (authorization != null) infos.Add(authorization.Value);
            }

            int ttl = _accountSettings is null
                ? AccountSettingsStore.DefaultAuthorizationTtlDays
                : await _accountSettings.GetAuthorizationTtlAsync(
                    auth.Value.AsAuthInfo().UserId);
            return GenerateAuthorizations(ttl, authKeyId, infos);
        }
}
