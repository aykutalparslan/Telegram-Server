// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Ferrite.Crypto;
using Ferrite.Data;
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

public sealed class GetPrivacyHandler : AccountHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;

    public GetPrivacyHandler(ISearchEngine search, IUpdatesService updates, IRandomGenerator random,
        IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChatRepository chatRepository, IPrivacyRulesRepository privacyRulesRepository, IUserRepository userRepository, IVerificationGateway verificationGateway)
        : base(search, updates, random, unitOfWork, chatRepository, privacyRulesRepository, userRepository, verificationGateway)
    {
        _authorizationRepository = authorizationRepository;

    }

    [TLFunction(Constructors.baseLayer_GetPrivacy)]
    public async ValueTask<TLPrivacyRules> Handle(long authKeyId, TLBytes q)
        {
            var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
            if (auth == null)
            {
                return (TLPrivacyRules)RpcErrorGenerator.GenerateError(400, "AUTH_KEY_INVALID"u8);
            }

            var key = GetPrivacyKey(((GetPrivacy)q).Key);
            if (key == null)
            {
                return (TLPrivacyRules)RpcErrorGenerator.GenerateError(400, "PRIVACY_KEY_INVALID"u8);
            }
            return await GetPrivacyRulesInternal(auth.Value, key.Value);
        }
}
