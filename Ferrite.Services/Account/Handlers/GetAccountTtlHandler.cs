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

public sealed class GetAccountTtlHandler : AccountHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IUserRepository _userRepository;

    public GetAccountTtlHandler(ISearchEngine search, IUpdatesService updates, IRandomGenerator random,
        IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChatRepository chatRepository, IPrivacyRulesRepository privacyRulesRepository, IUserRepository userRepository, IVerificationGateway verificationGateway)
        : base(search, updates, random, unitOfWork, chatRepository, privacyRulesRepository, userRepository, verificationGateway)
    {
        _authorizationRepository = authorizationRepository;
        _userRepository = userRepository;

    }

    [TLFunction(Constructors.baseLayer_GetAccountTTL)]
    public async ValueTask<TLAccountDaysTTL> Handle(long authKeyId, TLBytes q)
        {
            var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
            if (auth == null)
            {
                return (TLAccountDaysTTL)RpcErrorGenerator.GenerateError(400, "AUTH_KEY_INVALID"u8);
            }

            var ttlDays = _userRepository.GetAccountTtl(auth.Value.AsAuthInfo().UserId);
            return AccountDaysTTL.Builder().Days(ttlDays).Build();
        }
}
