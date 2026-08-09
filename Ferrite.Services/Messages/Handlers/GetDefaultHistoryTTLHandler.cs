// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class GetDefaultHistoryTTLHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ChatSettingsStore _settings;

    public GetDefaultHistoryTTLHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository,
        ChatSettingsStore settings)
    {
        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
        _settings = settings;
    }

    [TLFunction(Constructors.baseLayer_GetDefaultHistoryTTL)]
    public async ValueTask<TLDefaultHistoryTTL> Handle(long authKeyId, TLBytes q)
    {
        long userId;
        using (TLAuthInfo? auth = await _authorizationRepository
                   .GetAuthorizationAsync(authKeyId))
        {
            if (auth == null || !auth.Value.AsAuthInfo().LoggedIn)
            {
                return (TLDefaultHistoryTTL)RpcErrorGenerator.GenerateError(400,
                    Encoding.UTF8.GetBytes("AUTH_KEY_INVALID"));
            }
            userId = auth.Value.AsAuthInfo().UserId;
        }

        int period = await _settings.GetDefaultTtlPeriodAsync(userId);
        return DefaultHistoryTTL.Builder().Period(period).Build();
    }
}
