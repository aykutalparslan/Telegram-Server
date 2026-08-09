// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.users;
using TLUserFull = Ferrite.TL.baseLayer.users.TLUserFull;

namespace Ferrite.Services.Handlers.UserMethods;

public sealed class GetUsersHandler : UserHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;

    public GetUsersHandler(IUnitOfWork unitOfWork, IAppInfoRepository appInfoRepository, IAuthorizationRepository authorizationRepository, INotifySettingsRepository notifySettingsRepository, IPhotoRepository photoRepository, IUserRepository userRepository, IUserStatusRepository userStatusRepository, ProfileStore? profiles = null)
        : base(unitOfWork, appInfoRepository, notifySettingsRepository, photoRepository, userRepository, userStatusRepository, profiles)
    {
        _authorizationRepository = authorizationRepository;

    }

    [TLFunction(Constructors.baseLayer_GetUsers)]
    public async ValueTask<TLBytes> Handle(long authKeyId, TLBytes q)
        {
            List<InputUserRequest> requests = GetUserIds(q);
            long? selfUserId = null;
            var auth = await _authorizationRepository
                .GetAuthorizationAsync(authKeyId);
            if (auth != null)
            {
                selfUserId = auth.Value.AsAuthInfo().UserId;
            }

            List<byte[]> users = await GetUsersFromRepo(requests, selfUserId);
            var usersVector = new Vector();
            foreach (var user in users)
            {
                usersVector.AppendTLObject(user);
            }
            var usersBytes = usersVector.ToReadOnlySpan().ToArray();
            return new TLBytes(usersBytes, 0, usersBytes.Length);
        }
}
