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

public sealed class ResetAuthorizationHandler : AccountHandlerBase
{
    private readonly IAppInfoRepository _appInfoRepository;
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly ISecretChatAuthKeyCleanup _secretChatCleanup;

    public ResetAuthorizationHandler(ISearchEngine search, IUpdatesService updates, IRandomGenerator random,
        IUnitOfWork unitOfWork, IAppInfoRepository appInfoRepository, IAuthorizationRepository authorizationRepository, IChatRepository chatRepository, IPrivacyRulesRepository privacyRulesRepository, IUserRepository userRepository, IVerificationGateway verificationGateway,
        ISecretChatAuthKeyCleanup secretChatCleanup)
        : base(search, updates, random, unitOfWork, chatRepository, privacyRulesRepository, userRepository, verificationGateway)
    {
        _appInfoRepository = appInfoRepository;
        _authorizationRepository = authorizationRepository;

        _secretChatCleanup = secretChatCleanup;
    }

    [TLFunction(Constructors.baseLayer_ResetAuthorization)]
    public async ValueTask<TLBool> Handle(long authKeyId, TLBytes q)
        {
            long hash = ((ResetAuthorization)q).Hash;
            var sessAuthKeyId = _appInfoRepository.GetAuthKeyIdByAppHash(hash);
            if (sessAuthKeyId == null)
            {
                return (TLBool)RpcErrorGenerator.GenerateError(400, "HASH_INVALID"u8);
            }
            var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
            if(auth == null) return (TLBool)RpcErrorGenerator.GenerateError(400, "AUTH_KEY_INVALID"u8);
            if (DateTime.Now - DateTimeOffset.FromUnixTimeSeconds(auth.Value.AsAuthInfo().LoggedInAt) < new TimeSpan(1, 0, 0))
            {
                return (TLBool)RpcErrorGenerator.GenerateError(406, "FRESH_RESET_AUTHORISATION_FORBIDDEN"u8);
            }
            var info = await _authorizationRepository.GetAuthorizationAsync((long)sessAuthKeyId);
            if(info == null)
            {
                return (TLBool)RpcErrorGenerator.GenerateError(400, "HASH_INVALID"u8);
            }

            await _secretChatCleanup.CleanupAsync(sessAuthKeyId.Value);
            _authorizationRepository.DeleteAuthorization(sessAuthKeyId.Value);
            var result = await _unitOfWork.SaveAsync();
            return result ? new BoolTrue() : new BoolFalse();
        }
}
