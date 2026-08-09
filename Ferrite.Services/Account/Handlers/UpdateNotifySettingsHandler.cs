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

public sealed class UpdateNotifySettingsHandler : AccountHandlerBase
{
    private readonly IAppInfoRepository _appInfoRepository;
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly INotifySettingsRepository _notifySettingsRepository;

    public UpdateNotifySettingsHandler(ISearchEngine search, IUpdatesService updates, IRandomGenerator random,
        IUnitOfWork unitOfWork, IAppInfoRepository appInfoRepository, IAuthorizationRepository authorizationRepository, IChatRepository chatRepository, INotifySettingsRepository notifySettingsRepository, IPrivacyRulesRepository privacyRulesRepository, IUserRepository userRepository, IVerificationGateway verificationGateway)
        : base(search, updates, random, unitOfWork, chatRepository, privacyRulesRepository, userRepository, verificationGateway)
    {
        _appInfoRepository = appInfoRepository;
        _authorizationRepository = authorizationRepository;
        _notifySettingsRepository = notifySettingsRepository;

    }

    [TLFunction(Constructors.baseLayer_AccountUpdateNotifySettings)]
    public async ValueTask<TLBool> Handle(long authKeyId, TLBytes q)
        {
            var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
            if (auth == null)
            {
                return (TLBool)RpcErrorGenerator.GenerateError(400, "AUTH_KEY_INVALID"u8);
            }
            var info = _appInfoRepository.GetAppInfo(authKeyId);
            DeviceType deviceType = GetDeviceType(info);
            using var notifySettingsParameters = GetUpdateNotifySettingsParameters(deviceType, q);
            _notifySettingsRepository.PutNotifySettings(authKeyId,
                notifySettingsParameters.NotifyPeerType,
                notifySettingsParameters.PeerType,
                notifySettingsParameters.PeerId,
                (int)deviceType, notifySettingsParameters.PeerNotifySettings);
            var result = await _unitOfWork.SaveAsync();
            return result ? new BoolTrue() : new BoolFalse();
        }
}
