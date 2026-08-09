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

public sealed class ChangeAuthorizationSettingsHandler : AccountHandlerBase
{
    private readonly IAppInfoRepository _appInfoRepository;
    private readonly IAuthorizationRepository _authorizationRepository;

    public ChangeAuthorizationSettingsHandler(ISearchEngine search, IUpdatesService updates, IRandomGenerator random,
        IUnitOfWork unitOfWork, IAppInfoRepository appInfoRepository, IAuthorizationRepository authorizationRepository, IChatRepository chatRepository, IPrivacyRulesRepository privacyRulesRepository, IUserRepository userRepository, IVerificationGateway verificationGateway)
        : base(search, updates, random, unitOfWork, chatRepository, privacyRulesRepository, userRepository, verificationGateway)
    {
        _appInfoRepository = appInfoRepository;
        _authorizationRepository = authorizationRepository;

    }

    [TLFunction(Constructors.baseLayer_ChangeAuthorizationSettings)]
    public async ValueTask<TLBool> Handle(long authKeyId, TLBytes q)
        {
            var request = (ChangeAuthorizationSettings)q;
            long hash = request.Hash;
            bool? encryptedRequestsDisabled = request.Flags[0]
                ? request.EncryptedRequestsDisabled
                : null;
            bool? callRequestsDisabled = request.Flags[1]
                ? request.CallRequestsDisabled
                : null;
            var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
            if (auth == null)
            {
                return (TLBool)RpcErrorGenerator.GenerateError(400, "AUTH_KEY_INVALID"u8);
            }
            var appAuthKeyId = _appInfoRepository.GetAuthKeyIdByAppHash(hash);
            if(appAuthKeyId == null)
            {
                return (TLBool)RpcErrorGenerator.GenerateError(400, "HASH_INVALID"u8);
            }
            using var info = _appInfoRepository
                .GetAppInfo((long)appAuthKeyId);
            if (info == null) return new BoolFalse();
            var storedInfo = info.Value.AsAppInfo();
            using TLAppInfo newInfo = storedInfo.Clone()
                .EncryptedRequestsDisabled(encryptedRequestsDisabled ??
                                           storedInfo.EncryptedRequestsDisabled)
                .CallRequestsDisabled(callRequestsDisabled ??
                                      storedInfo.CallRequestsDisabled)
                .Build();
            var success = _appInfoRepository.PutAppInfo(newInfo);
            var result = success && await _unitOfWork.SaveAsync();
            return result ? new BoolTrue() : new BoolFalse();

        }
}
