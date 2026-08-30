// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Crypto;
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
        var importParameters = GetParameters(q);
        ExportSnapshot export;
        {
            TLExportedAuthInfo? resolved = await _authorizationRepository
                .GetExportedAuthorizationAsync(importParameters.UserId,
                    importParameters.Bytes);
            using TLExportedAuthInfo? exported = resolved;
            if (exported is not { } exportedRow)
            {
                return Error(400, "AUTH_BYTES_INVALID"u8);
            }
            var info = exportedRow.AsExportedAuthInfo();
            if (!importParameters.Bytes.AsSpan().SequenceEqual(info.Data))
            {
                return Error(400, "AUTH_BYTES_INVALID"u8);
            }
            export = new ExportSnapshot(info.AuthKeyId, info.UserId,
                info.Phone.ToArray());
        }

        {
            TLAuthInfo? resolved = await _authorizationRepository
                .GetAuthorizationAsync(authKeyId);
            using TLAuthInfo? current = resolved;
            if (current is { } currentRow)
            {
                var info = currentRow.AsAuthInfo();
                if (info.LoggedIn && info.UserId == export.UserId &&
                    info.Phone.SequenceEqual(export.Phone))
                {
                    return AuthorizationFor(export.UserId);
                }
                if (info.UserId != export.UserId ||
                    !info.Phone.SequenceEqual(export.Phone))
                {
                    return Error(400, "AUTH_BYTES_INVALID"u8);
                }
            }
        }

        bool persisted;
        {
            TLAuthInfo? resolved = await _authorizationRepository
                .GetAuthorizationAsync(export.SourceAuthKeyId);
            using TLAuthInfo? source = resolved;
            if (source is not { } sourceRow)
            {
                return Error(400, "AUTH_BYTES_INVALID"u8);
            }

            var info = sourceRow.AsAuthInfo();
            if (!info.LoggedIn || info.UserId != export.UserId ||
                !info.Phone.SequenceEqual(export.Phone))
            {
                return Error(400, "AUTH_BYTES_INVALID"u8);
            }

            using TLAuthInfo imported = info.Clone()
                .AuthKeyId(authKeyId)
                .Build();
            persisted = _authorizationRepository.PutAuthorization(imported);
        }

        if (!persisted || !await _unitOfWork.SaveAsync())
        {
            return Error(500, "INTERNAL_SERVER_ERROR"u8);
        }

        return AuthorizationFor(export.UserId);
    }

    private TLAuthorization AuthorizationFor(long userId)
    {
        TLUser? resolved = _userRepository.GetUser(userId);
        using TLUser? user = resolved;
        return user is { } value
            ? BuildAuthorization(value)
            : Error(400, "USER_ID_INVALID"u8);
    }

    private static TLAuthorization Error(int code, ReadOnlySpan<byte> message) =>
        (TLAuthorization)RpcErrorGenerator.GenerateError(code, message);

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
    private readonly record struct ExportSnapshot(long SourceAuthKeyId,
        long UserId, byte[] Phone);
}
