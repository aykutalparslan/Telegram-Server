// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Crypto;
using Ferrite.Data.Repositories;
using Ferrite.Services.Auth;
using Ferrite.TL;
using Ferrite.TL.baseLayer.auth;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.AuthMethods;

public sealed class LogOutHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IRandomGenerator _random;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISecretChatAuthKeyCleanup _secretChatCleanup;
    private readonly ILogger _log;

    public LogOutHandler(IRandomGenerator random, IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository,
        ISecretChatAuthKeyCleanup secretChatCleanup, ILogger log)
    {
        _authorizationRepository = authorizationRepository;

        _random = random;
        _unitOfWork = unitOfWork;
        _secretChatCleanup = secretChatCleanup;
        _log = log;
    }

    [TLFunction(Constructors.baseLayer_LogOut)]
    public async ValueTask<TLLoggedOut> Handle(long authKeyId, TLBytes q)
    {
        byte[] futureAuthToken = _random.GetRandomBytes(32);
        var info = await _authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        if (info == null)
        {
            return LoggedOut.Builder().Build();
        }

        long revokedAuthKeyId = info.Value.AsAuthInfo().AuthKeyId;
        var importedAuthKeyIds = await _authorizationRepository.GetAuthKeyIdsImportedFromAsync(
            Encoding.UTF8.GetString(info.Value.AsAuthInfo().Phone), revokedAuthKeyId);
        foreach (long importedAuthKeyId in importedAuthKeyIds)
        {
            await _secretChatCleanup.CleanupAsync(importedAuthKeyId);
            _authorizationRepository.DeleteAuthorization(importedAuthKeyId);
        }
        await _secretChatCleanup.CleanupAsync(revokedAuthKeyId);
        _authorizationRepository.DeleteAuthorization(revokedAuthKeyId);
        _log.Debug($"Log Out for authKey with Id: {authKeyId}");
        await _unitOfWork.SaveAsync();
        return LoggedOut.Builder().FutureAuthToken(futureAuthToken).Build();
    }
}
