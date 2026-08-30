// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.SecretChats.Handlers;

public sealed class ReceivedQueueHandler
{
    private readonly ISecretChatsRepository _secretChatsRepository;

    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IUpdatesContextFactory _updatesContextFactory;

    public ReceivedQueueHandler(IUnitOfWork unitOfWork, ISecretChatsRepository secretChatsRepository, IAuthorizationRepository authorizationRepository,
        IUpdatesContextFactory updatesContextFactory)
    {
        _secretChatsRepository = secretChatsRepository;

        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
        _updatesContextFactory = updatesContextFactory;
    }

    [TLFunction(Constructors.baseLayer_ReceivedQueue)]
    public async ValueTask<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var request = (ReceivedQueue)q;
        int maxQts = request.MaxQts;

        TLAuthInfo? authorization = await _authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        if (authorization == null)
        {
            return RpcErrorGenerator.GenerateError(400, "AUTH_KEY_INVALID"u8);
        }

        long userId;
        using (TLAuthInfo ownedAuthorization = authorization.Value)
        {
            userId = ownedAuthorization.AsAuthInfo().UserId;
        }

        IUpdatesContext updatesContext = _updatesContextFactory
            .GetUpdatesContext(authKeyId, userId);
        SecretChatQtsConfirmResult confirmation = await _secretChatsRepository.ConfirmQtsAsync(authKeyId, maxQts,
                updatesContext.Qts, updatesContext.IncrementQts);
        using TLSecretChatQtsState state = confirmation.State;
        if (confirmation.Status == SecretChatQtsConfirmStatus.Invalid)
        {
            return RpcErrorGenerator.GenerateError(400, "MAX_QTS_INVALID"u8);
        }

        var cancelledRandomIds = new VectorOfLong();
        byte[] bytes = cancelledRandomIds.ToReadOnlySpan().ToArray();
        return new TLBytes(bytes, 0, bytes.Length);
    }
}
