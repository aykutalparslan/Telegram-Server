// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL.baseLayer.updates;
using Ferrite.Utils;

namespace Ferrite.Services;

public sealed class UpdatesStateService : IUpdatesStateService
{
    private readonly IMessageRepository _messageRepository;
    private readonly IUpdatesStateRepository _updatesStateRepository;

    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IMTProtoTime _time;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IUpdatesContextFactory _updatesContextFactory;

    public UpdatesStateService(IMTProtoTime time, IUnitOfWork unitOfWork, IMessageRepository messageRepository, IUpdatesStateRepository updatesStateRepository, IAuthorizationRepository authorizationRepository,
        IUpdatesContextFactory updatesContextFactory)
    {
        _messageRepository = messageRepository;
        _updatesStateRepository = updatesStateRepository;

        _authorizationRepository = authorizationRepository;

        _time = time;
        _unitOfWork = unitOfWork;
        _updatesContextFactory = updatesContextFactory;
    }

    public async Task<TLState> GetState(long authKeyId)
    {
        var auth = await _authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        var context = _updatesContextFactory.GetUpdatesContext(authKeyId,
            auth.Value.AsAuthInfo().UserId);
        int date = (int)_time.GetUnixTimeInSeconds();
        int pts = await CommonUpdatesState.GetCommittedPts(_updatesStateRepository, _messageRepository, context,
            auth.Value.AsAuthInfo().UserId);
        int seq = await context.Seq();
        int qts = await context.Qts();
        int unreadCount = await context.UnreadMessages();
        return State.Builder().Date(date).Pts(pts).Seq(seq).Qts(qts)
            .UnreadCount(unreadCount).Build();
    }
}
