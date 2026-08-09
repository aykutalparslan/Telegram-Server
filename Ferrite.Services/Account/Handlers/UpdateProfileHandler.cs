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

public sealed class UpdateProfileHandler : AccountHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IUserRepository _userRepository;

    public UpdateProfileHandler(ISearchEngine search, IUpdatesService updates, IRandomGenerator random,
        IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChatRepository chatRepository, IPrivacyRulesRepository privacyRulesRepository, IUserRepository userRepository, IVerificationGateway verificationGateway)
        : base(search, updates, random, unitOfWork, chatRepository, privacyRulesRepository, userRepository, verificationGateway)
    {
        _authorizationRepository = authorizationRepository;
        _userRepository = userRepository;

    }

    [TLFunction(Constructors.baseLayer_UpdateProfile)]
    public async ValueTask<TLUser> Handle(long authKeyId, TLBytes q)
        {
            var request = (UpdateProfile)q;
            string? firstName = request.Flags[0] ? Encoding.UTF8.GetString(request.FirstName) : null;
            string? lastName = request.Flags[1] ? Encoding.UTF8.GetString(request.LastName) : null;
            string? about = request.Flags[2] ? Encoding.UTF8.GetString(request.About) : null;
            var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
            if (auth != null)
            {
                var userId = auth.Value.AsAuthInfo().UserId;
                using var u = _userRepository.GetUser(userId);
                if (u == null) return (TLUser)RpcErrorGenerator.GenerateError(400, "USER_ID_INVALID"u8);
                var user = ModifyUser(u.Value, firstName, lastName);
                _userRepository.PutUser(user);
                var userInfo = GetUserInfo(user);
                if (about != null) _userRepository.PutAbout(userInfo.UserId, about);
                var saved = await _unitOfWork.SaveAsync();
                if (saved)
                {
                    await IndexUser(userInfo);
                    if (firstName != null || lastName != null)
                    {
                        await EnqueueUserNameUpdate(userInfo);
                    }
                    if (about != null)
                    {
                        await EnqueueUserInvalidationUpdate(userInfo.UserId);
                    }
                }
                return user;
            }

            return (TLUser)RpcErrorGenerator.GenerateError(400,"FIRSTNAME_INVALID"u8);
        }
}
