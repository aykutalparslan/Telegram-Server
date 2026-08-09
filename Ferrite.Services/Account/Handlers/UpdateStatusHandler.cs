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

public sealed class UpdateStatusHandler : AccountHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IUserStatusRepository _userStatusRepository;

    private readonly ScheduledMessageRuntime _scheduledMessages;

    public UpdateStatusHandler(ISearchEngine search, IUpdatesService updates, IRandomGenerator random,
        IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChatRepository chatRepository, IPrivacyRulesRepository privacyRulesRepository, IUserRepository userRepository, IUserStatusRepository userStatusRepository, IVerificationGateway verificationGateway,
        ScheduledMessageRuntime scheduledMessages)
        : base(search, updates, random, unitOfWork, chatRepository, privacyRulesRepository, userRepository, verificationGateway)
    {
        _authorizationRepository = authorizationRepository;
        _userStatusRepository = userStatusRepository;

        _scheduledMessages = scheduledMessages;
    }

    [TLFunction(Constructors.baseLayer_UpdateStatus)]
    public async ValueTask<TLBool> Handle(long authKeyId, TLBytes q)
        {
            bool offline = ((UpdateStatus)q).Offline;
            var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
            if (auth != null)
            {
                var userId = auth.Value.AsAuthInfo().UserId;
                var now = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
                var result = _userStatusRepository.PutUserStatus(userId, !offline);
                var saved = result && await _unitOfWork.SaveAsync();
                if (saved)
                {
                    await EnqueueUserStatusUpdate(userId, offline, now);
                    if (!offline)
                    {
                        // A "send when online" scheduled entry has no date to wait
                        // for, only this transition. Its send date is the reserved
                        // 2147483646 sentinel, so nothing else would ever flush it.
                        await _scheduledMessages.FlushWhenOnlineAsync(userId);
                    }
                }
                return saved ? new BoolTrue() : new BoolFalse();
            }

            return new BoolFalse();
        }
}
