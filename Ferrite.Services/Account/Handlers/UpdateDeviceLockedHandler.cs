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

public sealed class UpdateDeviceLockedHandler : AccountHandlerBase
{
    private readonly IDeviceLockedRepository _deviceLockedRepository;

    public UpdateDeviceLockedHandler(ISearchEngine search, IUpdatesService updates, IRandomGenerator random,
        IUnitOfWork unitOfWork, IChatRepository chatRepository, IDeviceLockedRepository deviceLockedRepository, IPrivacyRulesRepository privacyRulesRepository, IUserRepository userRepository, IVerificationGateway verificationGateway)
        : base(search, updates, random, unitOfWork, chatRepository, privacyRulesRepository, userRepository, verificationGateway)
    {
        _deviceLockedRepository = deviceLockedRepository;

    }

    [TLFunction(Constructors.baseLayer_UpdateDeviceLocked)]
    public async ValueTask<TLBool> Handle(long authKeyId, TLBytes q)
        {
             int period = ((UpdateDeviceLocked)q).Period;
             _deviceLockedRepository.PutDeviceLocked(authKeyId, TimeSpan.FromSeconds(period));
             var result = await _unitOfWork.SaveAsync();
             return result ? new BoolTrue() : new BoolFalse();
        }
}
