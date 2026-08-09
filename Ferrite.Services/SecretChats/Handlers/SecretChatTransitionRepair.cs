// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.SecretChats.Handlers;

public sealed class SecretChatTransitionRepair : SecretChatLifecycleHandlerBase,
    ISecretChatTransitionRepair
{
    private readonly ISecretChatsRepository _secretChatsRepository;

    public SecretChatTransitionRepair(IUnitOfWork unitOfWork, IBlockedPeersRepository blockedPeersRepository, IAuthorizationRepository authorizationRepository, ISecretChatsRepository secretChatsRepository,
        ISecretChatDeviceSelector deviceSelector,
        SecretChatControlDelivery controlDelivery, SecretChatLimits limits)
        : base(unitOfWork, blockedPeersRepository, authorizationRepository, secretChatsRepository, deviceSelector, controlDelivery, limits)
    {
        _secretChatsRepository = secretChatsRepository;

    }

    public async ValueTask RepairAsync(long authKeyId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<TLDto.TLSecretChatState> targets = await _secretChatsRepository.GetControlTargetChatsAsync(authKeyId,
                cancellationToken);
        foreach (TLDto.TLSecretChatState target in targets)
        {
            using (target)
            {
                TLDto.SecretChatState row = target.AsSecretChatState();
                int chatId = row.ChatId;
                int updatedAt = row.UpdatedAt;
                SecretChatPersistenceState state =
                    (SecretChatPersistenceState)row.State;
                bool durable;
                switch (state)
                {
                    case SecretChatPersistenceState.Pending:
                        durable = await EnsureRequestedControlsAsync(target,
                            cancellationToken);
                        break;
                    case SecretChatPersistenceState.Active:
                        durable = await EnsureAcceptedControlsAsync(target,
                            cancellationToken);
                        break;
                    case SecretChatPersistenceState.Discarded:
                        IReadOnlyList<long> notificationAuthKeyIds = await _secretChatsRepository.GetControlTargetAuthKeyIdsAsync(
                                chatId, cancellationToken);
                        durable = await EnsureDiscardedControlsAsync(target, 0,
                            notificationAuthKeyIds, cancellationToken);
                        break;
                    default:
                        durable = false;
                        break;
                }

                if (durable)
                {
                    await _secretChatsRepository
                        .CompleteControlTransitionAsync(chatId, state, updatedAt,
                            cancellationToken);
                }
            }
        }
    }
}
