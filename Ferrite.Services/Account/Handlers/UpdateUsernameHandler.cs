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

public sealed class UpdateUsernameHandler : AccountHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IUserRepository _userRepository;

    private readonly ProfileStore? _profiles;

    public UpdateUsernameHandler(ISearchEngine search, IUpdatesService updates, IRandomGenerator random,
        IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChatRepository chatRepository, IPrivacyRulesRepository privacyRulesRepository, IUserRepository userRepository, IVerificationGateway verificationGateway,
        ProfileStore? profiles = null)
        : base(search, updates, random, unitOfWork, chatRepository, privacyRulesRepository, userRepository, verificationGateway)
    {
        _authorizationRepository = authorizationRepository;
        _userRepository = userRepository;

        _profiles = profiles;
    }

    [TLFunction(Constructors.baseLayer_AccountUpdateUsername)]
    public async ValueTask<TLUser> Handle(long authKeyId, TLBytes q)
        {
            string username = Encoding.UTF8.GetString(((AccountUpdateUsername)q).Username);
            if (!UsernameRegex.IsMatch(username))
            {
                return (TLUser)RpcErrorGenerator.GenerateError(400, "USERNAME_INVALID"u8);
            }
            var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
            if (auth == null)
            {
                return (TLUser)RpcErrorGenerator.GenerateError(400, "AUTH_KEY_INVALID"u8);
            }
            var user = _userRepository.GetUserByUsername(username);
            if (user == null)
            {
                _userRepository.UpdateUsername(auth.Value.AsAuthInfo().UserId, username);
            }
            else
            {
                return (TLUser)RpcErrorGenerator.GenerateError(400, "USERNAME_OCCUPIED"u8);
            }

            var saved = await _unitOfWork.SaveAsync();
            if (saved && _profiles is not null)
            {
                saved = await _profiles.SyncPrimaryUsernameAsync(
                    auth.Value.AsAuthInfo().UserId, username);
            }
            user = _userRepository.GetUser(auth.Value.AsAuthInfo().UserId);
            if(user == null) return (TLUser)RpcErrorGenerator.GenerateError(400, "USERNAME_NOT_MODIFIED"u8);
            var userInfo = GetUserInfo(user.Value);
            if (saved)
            {
                await IndexUser(userInfo);
                await EnqueueUserNameUpdate(userInfo);
            }
            return user.Value;
        }
}
