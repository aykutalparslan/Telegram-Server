// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

/// <summary>
/// Stores the account-wide default auto-delete period. Existing conversations
/// keep whatever they were given; the default only seeds new ones.
/// </summary>
public sealed class SetDefaultHistoryTTLHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ChatSettingsStore _settings;

    public SetDefaultHistoryTTLHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository,
        ChatSettingsStore settings)
    {
        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
        _settings = settings;
    }

    [TLFunction(Constructors.baseLayer_SetDefaultHistoryTTL)]
    public async ValueTask<TLBool> Handle(long authKeyId, TLBytes q)
    {
        long userId;
        using (TLAuthInfo? auth = await _authorizationRepository
                   .GetAuthorizationAsync(authKeyId))
        {
            if (auth == null || !auth.Value.AsAuthInfo().LoggedIn)
            {
                return Error("AUTH_KEY_INVALID");
            }
            userId = auth.Value.AsAuthInfo().UserId;
        }

        int period = ((SetDefaultHistoryTTL)q).Period;
        if (period < 0)
        {
            return Error("TTL_PERIOD_INVALID");
        }

        _settings.PutDefaultTtlPeriod(userId, period);
        return await _unitOfWork.SaveAsync()
            ? new BoolTrue()
            : Error("INTERNAL_SERVER_ERROR");
    }

    private static TLBool Error(string message) =>
        (TLBool)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));
}
