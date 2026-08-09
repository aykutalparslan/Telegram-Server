// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Crypto;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Services.Gateway;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.auth;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.mtproto;
using Ferrite.Utils;
using xxHash;
using TLAuthorization = Ferrite.TL.baseLayer.auth.TLAuthorization;

namespace Ferrite.Services.Handlers.AuthMethods;

public sealed class ImportAuthorizationHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IUserRepository _userRepository;

    private readonly IUnitOfWork _unitOfWork;

    public ImportAuthorizationHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IUserRepository userRepository)
    {
        _authorizationRepository = authorizationRepository;
        _userRepository = userRepository;

        _unitOfWork = unitOfWork;
    }

    [TLFunction(Constructors.baseLayer_ImportAuthorization)]
    public async ValueTask<TLAuthorization> Handle(long authKeyId, TLBytes q)
        {
            var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
            var importParameters = GetParameters(q);
            var exported = await _authorizationRepository
                .GetExportedAuthorizationAsync(importParameters.UserId, importParameters.Bytes);

            if (auth != null && exported != null &&
                auth.Value.AsAuthInfo().Phone.SequenceEqual(exported.Value.AsExportedAuthInfo().Phone) &&
                importParameters.Bytes.AsSpan().SequenceEqual(exported.Value.AsExportedAuthInfo().Data))
            {
                var user = _userRepository.GetUser(auth.Value.AsAuthInfo().UserId);
                if(user == null)
                {
                    return (TLAuthorization)RpcErrorGenerator.GenerateError(400, "USER_ID_INVALID"u8);
                }

                return BuildAuthorization(user.Value);
            }
            return (TLAuthorization)RpcErrorGenerator.GenerateError(400, "AUTH_BYTES_INVALID"u8);
        }

    private static ImportAuthorizationParameters GetParameters(TLBytes q)
    {
        var request = new ImportAuthorization(q.AsSpan());
        return new ImportAuthorizationParameters(request.Id, request.Bytes.ToArray());
    }

    private static TLAuthorization BuildAuthorization(TLBytes user)
    {
        using var modified = new User(user.AsSpan()).Clone().Self(true).Build();
        return AuthAuthorization.Builder().User(modified.ToReadOnlySpan()).Build();
    }

    private readonly record struct ImportAuthorizationParameters(long UserId, byte[] Bytes);
}
