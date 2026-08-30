// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.SecretChats.Handlers;

public enum SecretChatPeerResolutionStatus
{
    Resolved,
    Invalid,
    Declined,
    UserDeleted,
    UserBlocked
}

public readonly record struct SecretChatPeerContext(int ChatId, long AccessHash,
    long CallerUserId, long PeerUserId, long CallerAuthKeyId, long PeerAuthKeyId);

public readonly record struct SecretChatPeerResolution(
    SecretChatPeerResolutionStatus Status, SecretChatPeerContext Context);

public abstract class SecretChatHandlerBase
{
    private readonly IBlockedPeersRepository _blockedPeersRepository;

    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly ISecretChatsRepository _secretChatsRepository;
    private readonly IUserRepository _userRepository;

    protected readonly IUnitOfWork UnitOfWork;
    protected readonly SecretChatLimits Limits;

    protected SecretChatHandlerBase(IUnitOfWork unitOfWork, IBlockedPeersRepository blockedPeersRepository, IAuthorizationRepository authorizationRepository, ISecretChatsRepository secretChatsRepository, IUserRepository userRepository,
        SecretChatLimits limits)
    {
        _blockedPeersRepository = blockedPeersRepository;

        _authorizationRepository = authorizationRepository;
        _secretChatsRepository = secretChatsRepository;
        _userRepository = userRepository;

        UnitOfWork = unitOfWork;
        Limits = limits;
    }

    protected async ValueTask<SecretChatPeerResolution> ResolveActivePeerAsync(
        long authKeyId, int chatId, long accessHash, bool validateUser = true,
        bool validateBlocks = true)
    {
        TLDto.TLAuthInfo? authorization = await _authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        if (authorization is null)
        {
            return Invalid();
        }
        long callerUserId;
        using (TLDto.TLAuthInfo ownedAuthorization = authorization.Value)
        {
            TLDto.AuthInfo auth = ownedAuthorization.AsAuthInfo();
            if (!auth.LoggedIn)
            {
                return Invalid();
            }
            callerUserId = auth.UserId;
        }

        TLDto.TLSecretChatState? chatValue = await _secretChatsRepository
            .GetChatAsync(chatId);
        if (chatValue is null)
        {
            return Invalid();
        }

        long peerUserId;
        long peerAuthKeyId;
        using (TLDto.TLSecretChatState chat = chatValue.Value)
        {
            TLDto.SecretChatState row = chat.AsSecretChatState();
            if (row.AccessHash != accessHash || row.ChatId != chatId)
            {
                return Invalid();
            }
            SecretChatPersistenceState state =
                (SecretChatPersistenceState)row.State;
            if (state == SecretChatPersistenceState.Discarded)
            {
                return new SecretChatPeerResolution(
                    SecretChatPeerResolutionStatus.Declined, default);
            }
            if (state != SecretChatPersistenceState.Active || !row.Flags[1])
            {
                return Invalid();
            }

            if (authKeyId == row.InitiatorAuthKeyId &&
                callerUserId == row.InitiatorUserId)
            {
                peerAuthKeyId = row.RecipientAuthKeyId;
                peerUserId = row.RecipientUserId;
            }
            else if (authKeyId == row.RecipientAuthKeyId &&
                     callerUserId == row.RecipientUserId)
            {
                peerAuthKeyId = row.InitiatorAuthKeyId;
                peerUserId = row.InitiatorUserId;
            }
            else
            {
                return Invalid();
            }
        }

        if (validateUser)
        {
            TLUser? peerValue = _userRepository.GetUser(peerUserId);
            if (peerValue is null)
            {
                return new SecretChatPeerResolution(
                    SecretChatPeerResolutionStatus.UserDeleted, default);
            }
            using TLUser peer = peerValue.Value;
            if (peer.Constructor != Constructors.baseLayer_User ||
                peer.AsUser().Deleted)
            {
                return new SecretChatPeerResolution(
                    SecretChatPeerResolutionStatus.UserDeleted, default);
            }
        }

        if (validateBlocks && (IsBlockedBy(callerUserId, peerUserId) ||
                               IsBlockedBy(peerUserId, callerUserId)))
        {
            return new SecretChatPeerResolution(
                SecretChatPeerResolutionStatus.UserBlocked, default);
        }

        return new SecretChatPeerResolution(
            SecretChatPeerResolutionStatus.Resolved,
            new SecretChatPeerContext(chatId, accessHash, callerUserId, peerUserId,
                authKeyId, peerAuthKeyId));
    }

    protected bool IsBlockedBy(long ownerUserId, long peerUserId)
    {
        foreach (TLDto.TLBlockedPeer blockedValue in _blockedPeersRepository.GetBlockedPeers(ownerUserId))
        {
            using (blockedValue)
            {
                TLDto.BlockedPeer row = blockedValue.AsBlockedPeer();
                if (row.PeerType == (int)PeerType.User && row.PeerId == peerUserId)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static SecretChatPeerResolution Invalid() =>
        new(SecretChatPeerResolutionStatus.Invalid, default);
}
