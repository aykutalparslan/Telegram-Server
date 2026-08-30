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

public sealed class RegisterDeviceL57Handler : AccountHandlerBase
{
    private readonly IDeviceInfoRepository _deviceInfoRepository;

    public RegisterDeviceL57Handler(ISearchEngine search, IUpdatesService updates, IRandomGenerator random,
        IUnitOfWork unitOfWork, IChatRepository chatRepository, IDeviceInfoRepository deviceInfoRepository, IPrivacyRulesRepository privacyRulesRepository, IUserRepository userRepository, IVerificationGateway verificationGateway)
        : base(search, updates, random, unitOfWork, chatRepository, privacyRulesRepository, userRepository, verificationGateway)
    {
        _deviceInfoRepository = deviceInfoRepository;

    }

    [TLFunction(Constructors.baseLayer_RegisterDeviceL57)]
    public async ValueTask<TLBool> Handle(long authKeyId, TLBytes q)
        {
            using var deviceInfo = GetDeviceInfoL57(q);
            _deviceInfoRepository.PutDeviceInfo(authKeyId, deviceInfo);
            var result = await _unitOfWork.SaveAsync();
            return result ? new BoolTrue() : new BoolFalse();
        }
}
