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

public sealed class DeleteAccountHandler : AccountHandlerBase
{
    private readonly IAccountPasswordRepository _accountPasswordRepository;

    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IDeviceInfoRepository _deviceInfoRepository;
    private readonly INotifySettingsRepository _notifySettingsRepository;
    private readonly IPrivacyRulesRepository _privacyRulesRepository;
    private readonly IUserRepository _userRepository;

    private readonly IAccountPasswordManager _passwords;

    public DeleteAccountHandler(ISearchEngine search, IUpdatesService updates, IRandomGenerator random,
        IUnitOfWork unitOfWork, IAccountPasswordRepository accountPasswordRepository, IAuthorizationRepository authorizationRepository, IChatRepository chatRepository, IDeviceInfoRepository deviceInfoRepository, INotifySettingsRepository notifySettingsRepository, IPrivacyRulesRepository privacyRulesRepository, IUserRepository userRepository, IVerificationGateway verificationGateway,
        IAccountPasswordManager passwords)
        : base(search, updates, random, unitOfWork, chatRepository, privacyRulesRepository, userRepository, verificationGateway)
    {
        _accountPasswordRepository = accountPasswordRepository;

        _authorizationRepository = authorizationRepository;
        _deviceInfoRepository = deviceInfoRepository;
        _notifySettingsRepository = notifySettingsRepository;
        _privacyRulesRepository = privacyRulesRepository;
        _userRepository = userRepository;

        _passwords = passwords;
    }

    [TLFunction(Constructors.baseLayer_DeleteAccount)]
    public async ValueTask<TLBool> Handle(long authKeyId, TLBytes q)
    {
        var request = new DeleteAccount(q.AsSpan());
        bool hasPasswordProof = request.Flags[0];
        TLInputCheckPasswordSRP password = hasPasswordProof
            ? request.Get_Password()
            : default;

        TLAuthInfo? foundAuth = await _authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        if (foundAuth is not { } auth)
        {
            return new BoolFalse();
        }

        using (auth)
        {
            var authView = auth.AsAuthInfo();
            long authorizedUserId = authView.UserId;
            string authorizedPhone = Encoding.UTF8.GetString(authView.Phone);
            TLAccountPasswordState? foundPassword = await _accountPasswordRepository.GetPasswordStateAsync(
                    authorizedUserId);
            using (foundPassword)
            {
                if (foundPassword is { } passwordState &&
                    passwordState.AsAccountPasswordState().HasPassword)
                {
                    if (!hasPasswordProof)
                    {
                        return (TLBool)RpcErrorGenerator.GenerateError(400,
                            "PASSWORD_HASH_INVALID"u8);
                    }

                    using TLPasswordSettings verified = await _passwords
                        .GetPasswordSettingsAsync(authKeyId, password);
                    if (verified.Type == TLPasswordSettings
                            .PasswordSettingsType.RpcError)
                    {
                        return (TLBool)RpcErrorGenerator.GenerateError(400,
                            "PASSWORD_HASH_INVALID"u8);
                    }
                }
            }

            var authorizations = await _authorizationRepository
                .GetAuthorizationsAsync(authorizedPhone);
            TLUser? foundUser = _userRepository.GetUser(
                authorizedUserId);
            if (foundUser is not { } user)
            {
                return new BoolFalse();
            }

            foreach (var a in authorizations)
            {
                using (a)
                {
                    var keyId = a.AsAuthInfo().AuthKeyId;
                    _authorizationRepository.DeleteAuthorization(keyId);
                    TLDeviceInfo? foundDevice = _deviceInfoRepository
                        .GetDeviceInfo(keyId);
                    using (foundDevice)
                    {
                        if (foundDevice is { } device)
                        {
                            var deviceView = device.AsDeviceInfo();
                            var otherUIds = deviceView.OtherUids.ToArray();
                            _deviceInfoRepository.DeleteDeviceInfo(
                                keyId, Encoding.UTF8.GetString(deviceView.Token),
                                otherUIds);
                        }
                    }
                    _notifySettingsRepository.DeleteNotifySettings(keyId);
                    await _unitOfWork.SaveAsync();
                }
            }

            using (user)
            {
                long userId = user.AsUser().Id;
                _privacyRulesRepository.DeletePrivacyRules(userId);
                _userRepository.DeleteUser(userId);
                await _unitOfWork.SaveAsync();
                await _search.DeleteUser(userId);
            }

            return new BoolTrue();
        }
    }
}
