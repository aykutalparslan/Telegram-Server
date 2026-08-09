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

public sealed class GetNotifySettingsHandler : AccountHandlerBase
{
    private readonly INotifySettingsRepository _notifySettingsRepository;

    private readonly IAppInfoRepository _appInfoRepository;
    private readonly IAuthorizationRepository _authorizationRepository;

    public GetNotifySettingsHandler(ISearchEngine search, IUpdatesService updates, IRandomGenerator random,
        IUnitOfWork unitOfWork, INotifySettingsRepository notifySettingsRepository, IAppInfoRepository appInfoRepository, IAuthorizationRepository authorizationRepository, IChatRepository chatRepository, IPrivacyRulesRepository privacyRulesRepository, IUserRepository userRepository, IVerificationGateway verificationGateway)
        : base(search, updates, random, unitOfWork, chatRepository, privacyRulesRepository, userRepository, verificationGateway)
    {
        _notifySettingsRepository = notifySettingsRepository;

        _appInfoRepository = appInfoRepository;
        _authorizationRepository = authorizationRepository;

    }

    [TLFunction(Constructors.baseLayer_GetNotifySettings)]
    public async ValueTask<TLPeerNotifySettings> Handle(long authKeyId, TLBytes q)
        {
            var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
            if (auth == null)
            {
                return (TLPeerNotifySettings)RpcErrorGenerator.GenerateError(400, "AUTH_KEY_INVALID"u8);
            }

            (var peerId, InputNotifyPeerType notifyPeerType, InputPeerType peerType) =
                GetNotifyPeerInfo(new GetNotifySettings(q.AsSpan()).Peer);
            var info = _appInfoRepository.GetAppInfo(authKeyId);
            DeviceType deviceType = GetDeviceType(info);

            var settings = _notifySettingsRepository.GetNotifySettings(authKeyId,
                    (int)notifyPeerType, (int)peerType, peerId, (int)deviceType);
            if (settings.Count == 0)
            {
                return PeerNotifySettings.Builder().Build();
            }
            return (TLPeerNotifySettings)settings.First();
        }
}
