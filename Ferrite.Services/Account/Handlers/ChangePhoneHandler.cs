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

public sealed class ChangePhoneHandler : AccountHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IPhoneCodeRepository _phoneCodeRepository;
    private readonly IUserRepository _userRepository;

    public ChangePhoneHandler(ISearchEngine search, IUpdatesService updates, IRandomGenerator random,
        IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChatRepository chatRepository, IPhoneCodeRepository phoneCodeRepository, IPrivacyRulesRepository privacyRulesRepository, IUserRepository userRepository, IVerificationGateway verificationGateway)
        : base(search, updates, random, unitOfWork, chatRepository, privacyRulesRepository, userRepository, verificationGateway)
    {
        _authorizationRepository = authorizationRepository;
        _phoneCodeRepository = phoneCodeRepository;
        _userRepository = userRepository;

    }

    [TLFunction(Constructors.baseLayer_ChangePhone)]
    public async ValueTask<TLUser> Handle(long authKeyId, TLBytes q)
        {
            var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
            if(auth == null) return (TLUser)RpcErrorGenerator.GenerateError(400, "AUTH_KEY_INVALID"u8);
            var phoneNumber = Encoding.UTF8.GetString(((ChangePhone)q).PhoneNumber);
            var phoneCodeHash = Encoding.UTF8.GetString(((ChangePhone)q).PhoneCodeHash);
            var phoneCode = Encoding.UTF8.GetString(((ChangePhone)q).PhoneCode);
            var code = _phoneCodeRepository.GetPhoneCode(phoneNumber, phoneCodeHash);
            if (phoneCode != code)
            {
                return (TLUser)RpcErrorGenerator.GenerateError(400, "PHONE_CODE_EXPIRED"u8);
            }

            var user = _userRepository.GetUser(phoneNumber);
            if (user != null)
            {
                return (TLUser)RpcErrorGenerator.GenerateError(400, "PHONE_NUMBER_OCCUPIED"u8);
            }
            var authorizations = await _authorizationRepository.GetAuthorizationsAsync(Encoding.UTF8.GetString(auth.Value.AsAuthInfo().Phone));
            foreach (var authorization in authorizations)
            {
                using TLAuthInfo newAuth = authorization.AsAuthInfo()
                    .Clone()
                    .Phone(Encoding.UTF8.GetBytes(phoneNumber))
                    .Build();
                _authorizationRepository.PutAuthorization(newAuth);
            }
            _userRepository.UpdateUserPhone(auth.Value.AsAuthInfo().UserId, phoneNumber);
            await _unitOfWork.SaveAsync();
            user = _userRepository.GetUser(phoneNumber);
            await _unitOfWork.SaveAsync();
            return user!.Value;
        }
}
