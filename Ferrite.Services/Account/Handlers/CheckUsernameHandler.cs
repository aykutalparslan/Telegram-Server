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

public sealed class CheckUsernameHandler : AccountHandlerBase
{
    private readonly IUserRepository _userRepository;

    public CheckUsernameHandler(ISearchEngine search, IUpdatesService updates, IRandomGenerator random,
        IUnitOfWork unitOfWork, IChatRepository chatRepository, IPrivacyRulesRepository privacyRulesRepository, IUserRepository userRepository, IVerificationGateway verificationGateway)
        : base(search, updates, random, unitOfWork, chatRepository, privacyRulesRepository, userRepository, verificationGateway)
    {
        _userRepository = userRepository;

    }

    [TLFunction(Constructors.baseLayer_AccountCheckUsername)]
    public ValueTask<TLBool> Handle(long authKeyId, TLBytes q)
        {
            string username = Encoding.UTF8.GetString(((AccountCheckUsername)q).Username);
            if (!UsernameRegex.IsMatch(username))
            {
                return ValueTask.FromResult((TLBool)new BoolFalse());
            }

            var user = _userRepository.GetUserByUsername(username);
            if (user != null)
            {
                return ValueTask.FromResult((TLBool)new BoolFalse());
            }

            return ValueTask.FromResult((TLBool)new BoolTrue());
        }
}
