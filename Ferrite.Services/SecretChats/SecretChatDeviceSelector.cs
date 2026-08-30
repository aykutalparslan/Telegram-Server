// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.SecretChats;

public sealed class SecretChatDeviceSelector : ISecretChatDeviceSelector
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IAppInfoRepository _appInfoRepository;
    private readonly IUserRepository _userRepository;

    private readonly IUnitOfWork _unitOfWork;

    public SecretChatDeviceSelector(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IAppInfoRepository appInfoRepository, IUserRepository userRepository)
    {
        _authorizationRepository = authorizationRepository;

        _appInfoRepository = appInfoRepository;
        _userRepository = userRepository;

        _unitOfWork = unitOfWork;
    }

    public async ValueTask<IReadOnlyList<long>> GetEligibleAuthKeyIds(long userId)
    {
        using TLUser? user = _userRepository.GetUser(userId);
        if (user == null)
        {
            return Array.Empty<long>();
        }

        string phone = Encoding.UTF8.GetString(user.Value.AsUser().Phone);
        IReadOnlyList<TLAuthInfo> authorizations = await _authorizationRepository.GetAuthorizationsAsync(phone);
        var selected = new List<long>();
        var seen = new HashSet<long>();
        foreach (TLAuthInfo authorization in authorizations)
        {
            var auth = authorization.AsAuthInfo();
            if (auth.UserId != userId || !auth.LoggedIn || !seen.Add(auth.AuthKeyId))
            {
                continue;
            }

            TLAppInfo? appInfo = _appInfoRepository.GetAppInfo(auth.AuthKeyId);
            bool disabled = false;
            if (appInfo.HasValue)
            {
                using TLAppInfo ownedInfo = appInfo.Value;
                disabled = ownedInfo.AsAppInfo().EncryptedRequestsDisabled;
            }
            if (!disabled)
            {
                selected.Add(auth.AuthKeyId);
            }
        }

        return selected;
    }
}
