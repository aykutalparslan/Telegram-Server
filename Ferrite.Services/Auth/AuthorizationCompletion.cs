// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.auth;
using Ferrite.TL.baseLayer.dto;
using TLAuthorization = Ferrite.TL.baseLayer.auth.TLAuthorization;

namespace Ferrite.Services;

public interface IAuthorizationCompletion
{
    /// <summary>
    /// Resolves either a pending or completed authorization. The caller owns the
    /// returned value and must dispose it.
    /// </summary>
    ValueTask<TLAuthInfo?> ResolveAsync(long authKeyId);

    ValueTask<bool> CreateOrUpdatePendingAsync(long authKeyId, long userId,
        string phone, int apiLayer = -1);

    /// <summary>
    /// Completes a pending authorization and returns an owned auth.authorization.
    /// A null result means the authorization or user was missing, or persistence
    /// failed.
    /// </summary>
    ValueTask<TLAuthorization?> CompleteAsync(long authKeyId);
}

public sealed class AuthorizationCompletion : IAuthorizationCompletion
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IUserRepository _userRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _timeProvider;

    public AuthorizationCompletion(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IUserRepository userRepository, TimeProvider timeProvider)
    {
        _authorizationRepository = authorizationRepository;
        _userRepository = userRepository;

        _unitOfWork = unitOfWork;
        _timeProvider = timeProvider;
    }

    public ValueTask<TLAuthInfo?> ResolveAsync(long authKeyId) =>
        _authorizationRepository.GetAuthorizationAsync(authKeyId);

    public async ValueTask<bool> CreateOrUpdatePendingAsync(long authKeyId,
        long userId, string phone, int apiLayer = -1)
    {
        TLAuthInfo? resolved = await ResolveAsync(authKeyId);
        using TLAuthInfo? existing = resolved;

        byte[] phoneBytes = Encoding.UTF8.GetBytes(phone);
        using TLAuthInfo pending = existing is { } current
            ? BuildUpdatedPending(current, userId, phoneBytes)
            : AuthInfo.Builder()
                .AuthKeyId(authKeyId)
                .UserId(userId)
                .Phone(phoneBytes)
                .ApiLayer(apiLayer)
                .LoggedIn(false)
                .LoggedInAt(0)
                .Build();

        if (!_authorizationRepository.PutAuthorization(pending))
        {
            return false;
        }

        return await _unitOfWork.SaveAsync();
    }

    public async ValueTask<TLAuthorization?> CompleteAsync(long authKeyId)
    {
        TLAuthInfo? resolved = await ResolveAsync(authKeyId);
        if (resolved is not { } current)
        {
            return null;
        }

        using (current)
        {
            var info = current.AsAuthInfo();
            TLUser? resolvedUser = _userRepository.GetUser(info.UserId);
            if (resolvedUser is not { } user)
            {
                return null;
            }

            using (user)
            {
                if (!info.LoggedIn)
                {
                    using TLAuthInfo completed = info.Clone()
                        .LoggedIn(true)
                        .LoggedInAt((int)_timeProvider.GetUtcNow().ToUnixTimeSeconds())
                        .Build();

                    if (!_authorizationRepository
                            .PutAuthorization(completed) ||
                        !await _unitOfWork.SaveAsync())
                    {
                        return null;
                    }
                }

                return BuildAuthorization(user);
            }
        }
    }

    private static TLAuthInfo BuildUpdatedPending(TLAuthInfo existing, long userId,
        ReadOnlySpan<byte> phone)
    {
        var info = existing.AsAuthInfo();
        return info.Clone()
            .UserId(userId)
            .Phone(phone)
            .LoggedIn(false)
            .LoggedInAt(0)
            .Build();
    }

    private static TLAuthorization BuildAuthorization(TLUser user)
    {
        using var self = new User(user.AsSpan()).Clone().Self(true).Build();
        return AuthAuthorization.Builder()
            .User(self.ToReadOnlySpan())
            .Build();
    }
}
