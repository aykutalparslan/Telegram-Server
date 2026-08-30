// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Security.Cryptography;
using System.Text;
using Ferrite.Crypto;
using Ferrite.Data.Repositories;
using Ferrite.Services.Gateway;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.auth;
using Ferrite.TL.baseLayer.dto;
using TLAuthAuthorization = Ferrite.TL.baseLayer.auth.TLAuthorization;
using TLSessionAuthorization = Ferrite.TL.baseLayer.TLAuthorization;
using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Auth;

public interface ILoginTokenService
{
    ValueTask<TLLoginToken> ExportAsync(long authKeyId, long sessionId,
        int apiId, string apiHash, IReadOnlyCollection<long> excludedUserIds);

    ValueTask<TLLoginToken> ImportAsync(long authKeyId,
        ReadOnlyMemory<byte> token);

    ValueTask<TLSessionAuthorization> AcceptAsync(long authKeyId,
        ReadOnlyMemory<byte> token);

    ValueTask<TLAuthAuthorization> ImportWebAsync(long authKeyId, int apiId,
        string apiHash, string webAuthorizationToken);
}

public sealed class LoginTokenService : ILoginTokenService
{
    private readonly IAppInfoRepository _appInfoRepository;
    private readonly ILoginTokenRepository _loginTokenRepository;
    private readonly IUserRepository _userRepository;

    private const int PendingState = 0;
    private const int AcceptedState = 1;
    private static readonly TimeSpan QrTokenLifetime = TimeSpan.FromSeconds(30);

    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuthorizationCompletion _authorizationCompletion;
    private readonly IUpdatesService _updates;
    private readonly TimeProvider _timeProvider;
    private readonly IRandomGenerator _random;
    private readonly IWebAuthorizationTokenValidator _webTokenValidator;

    public LoginTokenService(IUnitOfWork unitOfWork, IAppInfoRepository appInfoRepository, ILoginTokenRepository loginTokenRepository, IUserRepository userRepository,
        IAuthorizationCompletion authorizationCompletion,
        IUpdatesService updates, TimeProvider timeProvider,
        IRandomGenerator random,
        IWebAuthorizationTokenValidator webTokenValidator)
    {
        _appInfoRepository = appInfoRepository;
        _loginTokenRepository = loginTokenRepository;
        _userRepository = userRepository;

        _unitOfWork = unitOfWork;
        _authorizationCompletion = authorizationCompletion;
        _updates = updates;
        _timeProvider = timeProvider;
        _random = random;
        _webTokenValidator = webTokenValidator;
    }

    public async ValueTask<TLLoginToken> ExportAsync(long authKeyId,
        long sessionId, int apiId, string apiHash,
        IReadOnlyCollection<long> excludedUserIds)
    {
        using TLAppInfo? exportingApp = GetMatchingApp(authKeyId, apiId);
        if (apiId <= 0 || exportingApp is null ||
            string.IsNullOrWhiteSpace(apiHash))
        {
            return LoginError("API_ID_INVALID"u8);
        }

        TLAuthInfo? resolved = await _authorizationCompletion.ResolveAsync(authKeyId);
        using (resolved)
        {
            if (resolved is { } current && current.AsAuthInfo().LoggedIn)
            {
                TLAuthAuthorization? authorization =
                    await _authorizationCompletion.CompleteAsync(authKeyId);
                using (authorization)
                {
                    if (authorization is not { } completed)
                    {
                        return LoginError("AUTH_KEY_INVALID"u8);
                    }

                    return LoginTokenSuccess.Builder()
                        .Authorization(completed.AsSpan())
                        .Build();
                }
            }
        }

        int now = UtcNow();
        int expiresAt = checked(now + (int)QrTokenLifetime.TotalSeconds);
        byte[] token = await CreateUniqueQrTokenAsync();
        VectorOfLong excluded = new();
        foreach (long userId in excludedUserIds)
        {
            excluded.Append(userId);
        }

        using TLDto.TLQrLoginToken row = QrLoginToken.Builder()
            .Token(token)
            .ExporterAuthKeyId(authKeyId)
            .ExporterSessionId(sessionId)
            .ApiId(apiId)
            .ApiHash(Encoding.UTF8.GetBytes(apiHash))
            .ExcludedUserIds(excluded)
            .State(PendingState)
            .CreatedAt(now)
            .ExpiresAt(expiresAt)
            .Build();
        await _loginTokenRepository.PutQrTokenAsync(row,
            QrTokenLifetime);

        return LoginToken.Builder()
            .Expires(expiresAt)
            .Token(token)
            .Build();
    }

    public async ValueTask<TLLoginToken> ImportAsync(long authKeyId,
        ReadOnlyMemory<byte> token)
    {
        TLDto.TLQrLoginToken? resolved = await _loginTokenRepository
            .GetQrTokenAsync(token);
        using (resolved)
        {
            if (resolved is not { } current)
            {
                return LoginError("AUTH_TOKEN_INVALID"u8);
            }

            var row = current.AsQrLoginToken();
            if (row.ExpiresAt <= UtcNow())
            {
                return LoginError("AUTH_TOKEN_EXPIRED"u8);
            }
            using TLAppInfo? importingApp = GetMatchingApp(authKeyId, row.ApiId);
            if (row.ExporterAuthKeyId != authKeyId || importingApp is null ||
                row.ApiHash.IsEmpty)
            {
                return LoginError("AUTH_TOKEN_INVALID"u8);
            }
            if (row.State == PendingState)
            {
                return LoginToken.Builder()
                    .Expires(row.ExpiresAt)
                    .Token(row.Token)
                    .Build();
            }
            if (row.State != AcceptedState || row.AcceptedUserId <= 0)
            {
                return LoginError("AUTH_TOKEN_INVALID"u8);
            }
        }

        TLDto.TLQrLoginToken? consumed = await _loginTokenRepository
            .ConsumeQrTokenAsync(token, AcceptedState);
        long acceptedUserId;
        using (consumed)
        {
            if (consumed is not { } accepted)
            {
                return LoginError("AUTH_TOKEN_INVALID"u8);
            }

            var row = accepted.AsQrLoginToken();
            if (row.ExpiresAt <= UtcNow() ||
                row.ExporterAuthKeyId != authKeyId ||
                row.AcceptedUserId <= 0)
            {
                return LoginError(row.ExpiresAt <= UtcNow()
                    ? "AUTH_TOKEN_EXPIRED"u8
                    : "AUTH_TOKEN_INVALID"u8);
            }
            acceptedUserId = row.AcceptedUserId;
        }

        TLAuthAuthorization? authorization = await PromoteAsync(authKeyId,
            acceptedUserId);
        using (authorization)
        {
            if (authorization is not { } completed)
            {
                return LoginError("AUTH_KEY_INVALID"u8);
            }

            return LoginTokenSuccess.Builder()
                .Authorization(completed.AsSpan())
                .Build();
        }
    }

    public async ValueTask<TLSessionAuthorization> AcceptAsync(long authKeyId,
        ReadOnlyMemory<byte> token)
    {
        TLAuthInfo? acceptingAuthorization =
            await _authorizationCompletion.ResolveAsync(authKeyId);
        long acceptingUserId;
        using (acceptingAuthorization)
        {
            if (acceptingAuthorization is not { } current ||
                !current.AsAuthInfo().LoggedIn)
            {
                return SessionError("AUTH_KEY_INVALID"u8);
            }
            acceptingUserId = current.AsAuthInfo().UserId;
        }

        TLUser? acceptingUser = _userRepository.GetUser(acceptingUserId);
        using (acceptingUser)
        {
            if (acceptingUser is null)
            {
                return SessionError("AUTH_KEY_INVALID"u8);
            }
        }

        TLDto.TLQrLoginToken? resolved = await _loginTokenRepository
            .GetQrTokenAsync(token);
        long acceptedUserId = 0;
        byte[] pendingToken;
        long exporterAuthKeyId;
        long exporterSessionId;
        int tokenApiId;
        byte[] tokenApiHash;
        long[] excludedUserIds;
        int state;
        int createdAt;
        int expiresAt;
        using (resolved)
        {
            if (resolved is not { } current)
            {
                return SessionError("AUTH_TOKEN_INVALID"u8);
            }

            var row = current.AsQrLoginToken();
            acceptedUserId = row.AcceptedUserId;
            pendingToken = row.Token.ToArray();
            exporterAuthKeyId = row.ExporterAuthKeyId;
            exporterSessionId = row.ExporterSessionId;
            tokenApiId = row.ApiId;
            tokenApiHash = row.ApiHash.ToArray();
            excludedUserIds = new long[row.ExcludedUserIds.Count];
            for (int i = 0; i < excludedUserIds.Length; i++)
            {
                excludedUserIds[i] = row.ExcludedUserIds[i];
            }
            state = row.State;
            createdAt = row.CreatedAt;
            expiresAt = row.ExpiresAt;
        }

        if (expiresAt <= UtcNow())
        {
            return SessionError("AUTH_TOKEN_EXPIRED"u8);
        }
        if (state == AcceptedState)
        {
            return SessionError("AUTH_TOKEN_ALREADY_ACCEPTED"u8);
        }
        using TLAppInfo? exporterApp = GetMatchingApp(exporterAuthKeyId,
            tokenApiId);
        if (state != PendingState || acceptedUserId != 0 ||
            tokenApiHash.Length == 0 ||
            exporterAuthKeyId == authKeyId ||
            excludedUserIds.Contains(acceptingUserId) || exporterApp is null)
        {
            return SessionError("AUTH_TOKEN_INVALID"u8);
        }

        TimeSpan remaining = TimeSpan.FromSeconds(expiresAt - UtcNow());
        VectorOfLong excluded = new();
        foreach (long userId in excludedUserIds)
        {
            excluded.Append(userId);
        }
        using TLDto.TLQrLoginToken accepted = QrLoginToken.Builder()
            .AcceptedUserId(acceptingUserId)
            .AcceptedAuthKeyId(authKeyId)
            .Token(pendingToken)
            .ExporterAuthKeyId(exporterAuthKeyId)
            .ExporterSessionId(exporterSessionId)
            .ApiId(tokenApiId)
            .ApiHash(tokenApiHash)
            .ExcludedUserIds(excluded)
            .State(AcceptedState)
            .CreatedAt(createdAt)
            .ExpiresAt(expiresAt)
            .Build();
        bool replaced = await _loginTokenRepository
            .TryReplaceQrTokenAsync(token, PendingState, accepted, remaining);
        if (!replaced)
        {
            return await AcceptanceRaceErrorAsync(token);
        }

        TLAuthAuthorization? promoted = await PromoteAsync(
            exporterAuthKeyId, acceptingUserId);
        using (promoted)
        {
            if (promoted is null)
            {
                return SessionError("AUTH_KEY_INVALID"u8);
            }
        }

        using TLUpdate update = UpdateLoginToken.Builder().Build();
        await _updates.EnqueueUpdate(acceptingUserId, update,
            UpdateDeliveryScope.ForAuthKey(exporterAuthKeyId));

        return BuildSessionAuthorization(exporterApp.Value, createdAt,
            UtcNow());
    }

    public async ValueTask<TLAuthAuthorization> ImportWebAsync(long authKeyId,
        int apiId, string apiHash, string webAuthorizationToken)
    {
        using TLAppInfo? importingApp = GetMatchingApp(authKeyId, apiId);
        if (apiId <= 0 || importingApp is null ||
            string.IsNullOrWhiteSpace(apiHash))
        {
            return AuthError("API_ID_INVALID"u8);
        }
        if (string.IsNullOrWhiteSpace(webAuthorizationToken))
        {
            return AuthError("AUTH_TOKEN_INVALID"u8);
        }

        WebAuthorizationTokenValidationResult validation =
            await _webTokenValidator.ValidateAsync(
                new WebAuthorizationTokenValidationRequest(apiId, apiHash,
                    webAuthorizationToken));
        if (!validation.IsValid)
        {
            return AuthError("AUTH_TOKEN_INVALID"u8);
        }

        byte[] digest = SHA256.HashData(
            Encoding.UTF8.GetBytes(webAuthorizationToken));
        byte[] requestedApiHash = Encoding.UTF8.GetBytes(apiHash);
        TLDto.TLWebAuthorizationToken? resolved = await _loginTokenRepository.GetWebTokenAsync(digest);
        using (resolved)
        {
            if (resolved is not { } current)
            {
                return AuthError("AUTH_TOKEN_INVALID"u8);
            }
            var row = current.AsWebAuthorizationToken();
            if (row.ExpiresAt <= UtcNow())
            {
                return AuthError("AUTH_TOKEN_EXPIRED"u8);
            }
            if (row.ApiId != apiId || row.UserId != validation.UserId ||
                !CryptographicOperations.FixedTimeEquals(row.ApiHash,
                    requestedApiHash))
            {
                return AuthError("AUTH_TOKEN_INVALID"u8);
            }
        }

        TLDto.TLWebAuthorizationToken? consumed = await _loginTokenRepository.ConsumeWebTokenAsync(digest);
        long userId;
        using (consumed)
        {
            if (consumed is not { } current)
            {
                return AuthError("AUTH_TOKEN_INVALID"u8);
            }
            var row = current.AsWebAuthorizationToken();
            if (row.ExpiresAt <= UtcNow())
            {
                return AuthError("AUTH_TOKEN_EXPIRED"u8);
            }
            if (row.ApiId != apiId || row.UserId != validation.UserId ||
                !CryptographicOperations.FixedTimeEquals(row.ApiHash,
                    requestedApiHash))
            {
                return AuthError("AUTH_TOKEN_INVALID"u8);
            }
            userId = row.UserId;
        }

        TLAuthAuthorization? authorization = await PromoteAsync(authKeyId,
            userId);
        using (authorization)
        {
            return authorization is { } completed
                ? CloneAuthorization(completed)
                : AuthError("AUTH_KEY_INVALID"u8);
        }
    }

    private async ValueTask<byte[]> CreateUniqueQrTokenAsync()
    {
        for (int attempt = 0; attempt < 4; attempt++)
        {
            byte[] token = _random.GetRandomBytes(32);
            TLDto.TLQrLoginToken? collision = await _loginTokenRepository.GetQrTokenAsync(token);
            using (collision)
            {
                if (collision is null)
                {
                    return token;
                }
            }
        }

        throw new InvalidOperationException(
            "Unable to allocate a unique QR login token.");
    }

    private async ValueTask<TLAuthAuthorization?> PromoteAsync(long authKeyId,
        long userId)
    {
        TLUser? resolvedUser = _userRepository.GetUser(userId);
        string phone;
        using (resolvedUser)
        {
            if (resolvedUser is not { } user)
            {
                return null;
            }
            phone = Encoding.UTF8.GetString(user.AsUser().Phone);
        }

        if (!await _authorizationCompletion.CreateOrUpdatePendingAsync(authKeyId,
                userId, phone))
        {
            return null;
        }
        return await _authorizationCompletion.CompleteAsync(authKeyId);
    }

    private async ValueTask<TLSessionAuthorization> AcceptanceRaceErrorAsync(
        ReadOnlyMemory<byte> token)
    {
        TLDto.TLQrLoginToken? current = await _loginTokenRepository
            .GetQrTokenAsync(token);
        using (current)
        {
            if (current is { } row && row.AsQrLoginToken().State == AcceptedState)
            {
                return SessionError("AUTH_TOKEN_ALREADY_ACCEPTED"u8);
            }
        }
        return SessionError("AUTH_TOKEN_INVALID"u8);
    }

    private TLAppInfo? GetMatchingApp(long authKeyId, int apiId)
    {
        TLAppInfo? resolved = _appInfoRepository.GetAppInfo(authKeyId);
        if (resolved is not { } current)
        {
            return null;
        }

        var app = current.AsAppInfo();
        if (app.AuthKeyId == authKeyId && app.ApiId == apiId)
        {
            return current;
        }

        current.Dispose();
        return null;
    }

    private static TLSessionAuthorization BuildSessionAuthorization(
        TLAppInfo appInfo, int createdAt, int activeAt)
    {
        var app = appInfo.AsAppInfo();
        return
        Authorization.Builder()
            .EncryptedRequestsDisabled(app.EncryptedRequestsDisabled)
            .CallRequestsDisabled(app.CallRequestsDisabled)
            .Hash(app.Hash)
            .DeviceModel(app.DeviceModel)
            .Platform("Unknown"u8)
            .SystemVersion(app.SystemVersion)
            .ApiId(app.ApiId)
            .AppName("Unknown"u8)
            .AppVersion(app.AppVersion)
            .DateCreated(createdAt)
            .DateActive(activeAt)
            .Ip(app.Ip)
            .Country("Unknown"u8)
            .Region("Unknown"u8)
            .Build();
    }

    private static TLAuthAuthorization CloneAuthorization(
        TLAuthAuthorization authorization) =>
        authorization.AsAuthAuthorization().Clone().Build();

    private int UtcNow() =>
        checked((int)_timeProvider.GetUtcNow().ToUnixTimeSeconds());

    private static TLLoginToken LoginError(ReadOnlySpan<byte> message) =>
        (TLLoginToken)RpcErrorGenerator.GenerateError(400, message);

    private static TLSessionAuthorization SessionError(
        ReadOnlySpan<byte> message) =>
        (TLSessionAuthorization)RpcErrorGenerator.GenerateError(400, message);

    private static TLAuthAuthorization AuthError(ReadOnlySpan<byte> message) =>
        (TLAuthAuthorization)RpcErrorGenerator.GenerateError(400, message);

}
