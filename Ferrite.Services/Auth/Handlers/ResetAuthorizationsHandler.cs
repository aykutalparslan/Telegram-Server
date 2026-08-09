// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;

namespace Ferrite.Services.Handlers.AuthMethods;

public sealed class ResetAuthorizationsHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ISecretChatAuthKeyCleanup _secretChatCleanup;

    public ResetAuthorizationsHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository,
        ISecretChatAuthKeyCleanup secretChatCleanup)
    {
        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
        _secretChatCleanup = secretChatCleanup;
    }

    [TLFunction(Constructors.baseLayer_ResetAuthorizations)]
    public async ValueTask<TLBool> Handle(long authKeyId, TLBytes q)
    {
        var currentAuth = await _authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        if (currentAuth == null)
        {
            return new BoolFalse();
        }

        var authorizations = await _authorizationRepository
            .GetAuthorizationsAsync(
                Encoding.UTF8.GetString(currentAuth.Value.AsAuthInfo().Phone));
        foreach (var auth in authorizations)
        {
            if (auth.AsAuthInfo().AuthKeyId != authKeyId)
            {
                long revokedAuthKeyId = auth.AsAuthInfo().AuthKeyId;
                await _secretChatCleanup.CleanupAsync(revokedAuthKeyId);
                _authorizationRepository.DeleteAuthorization(revokedAuthKeyId);
            }
        }

        await _unitOfWork.SaveAsync();
        return new BoolTrue();
    }
}
