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

public sealed class SendChangePhoneCodeHandler : AccountHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IPhoneCodeRepository _phoneCodeRepository;
    private readonly IUserRepository _userRepository;

    public SendChangePhoneCodeHandler(ISearchEngine search, IUpdatesService updates, IRandomGenerator random,
        IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChatRepository chatRepository, IPhoneCodeRepository phoneCodeRepository, IPrivacyRulesRepository privacyRulesRepository, IUserRepository userRepository, IVerificationGateway verificationGateway)
        : base(search, updates, random, unitOfWork, chatRepository, privacyRulesRepository, userRepository, verificationGateway)
    {
        _authorizationRepository = authorizationRepository;
        _phoneCodeRepository = phoneCodeRepository;
        _userRepository = userRepository;

    }

    [TLFunction(Constructors.baseLayer_SendChangePhoneCode)]
    public async ValueTask<TLSentCode> Handle(long authKeyId, TLBytes q)
        {
            var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
            if(auth == null) return (TLSentCode)RpcErrorGenerator.GenerateError(400, "AUTH_KEY_INVALID"u8);
            if (DateTime.Now - DateTimeOffset.FromUnixTimeSeconds(auth.Value.AsAuthInfo().LoggedInAt) < new TimeSpan(1, 0, 0))
            {
                return (TLSentCode)RpcErrorGenerator.GenerateError(406, "FRESH_CHANGE_PHONE_FORBIDDEN"u8);
            }
            var phoneNumber = Encoding.UTF8.GetString(((SendChangePhoneCode)q).PhoneNumber);
            var user = _userRepository.GetUser(phoneNumber);
            if (user != null)
            {
                return (TLSentCode)RpcErrorGenerator.GenerateError(406, "PHONE_NUMBER_OCCUPIED"u8);
            }

            var code = await _verificationGateway.SendSms(phoneNumber);
            Console.WriteLine("auth.sentCode=>" + code.ToString());
            var hash = GeneratePhoneCodeHash(code);

            _phoneCodeRepository.PutPhoneCode(phoneNumber, hash, code.ToString(),
                new TimeSpan(0, 0, PhoneCodeTimeout*2));
            await _unitOfWork.SaveAsync();

            return GenerateSentCode(hash);
        }
}
