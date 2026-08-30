// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System;
using System.Collections.ObjectModel;
using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.updates;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.UpdateMethods;

public sealed class GetStateHandler : UpdatesHandlerBase
{
    private readonly IMessageRepository _messageRepository;
    private readonly IUpdatesStateRepository _updatesStateRepository;

    private readonly IAuthorizationRepository _authorizationRepository;

    public GetStateHandler(IMTProtoTime time, IUnitOfWork unitOfWork, IMessageRepository messageRepository, IUpdatesStateRepository updatesStateRepository, IAuthorizationRepository authorizationRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository,
        IUpdatesContextFactory updatesContextFactory, ICounterFactory counterFactory,
        ILogger log)
        : base(time, unitOfWork, chatParticipantsRepository, chatRepository, updatesContextFactory, counterFactory, log)
    {
        _messageRepository = messageRepository;
        _updatesStateRepository = updatesStateRepository;

        _authorizationRepository = authorizationRepository;

    }

    [TLFunction(Constructors.baseLayer_GetState)]
    public async Task<TLState> Handle(long authKeyId, TLBytes q)
        {
            var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
            long userId = auth.Value.AsAuthInfo().UserId;
            var updatesCtx = _updatesContextFactory.GetUpdatesContext(authKeyId,
                userId);
            int date = (int)_time.GetUnixTimeInSeconds();
            int pts = await CommonUpdatesState.GetCommittedPts(_updatesStateRepository, _messageRepository, updatesCtx, userId);
            int qts = await updatesCtx.Qts();
            int seq = await updatesCtx.Seq();
            int unreadCount = await updatesCtx.UnreadMessages();
            var state = State.Builder().Date(date).Pts(pts).Seq(seq).Qts(qts)
                .UnreadCount(unreadCount).Build();
            _log.Debug($"/// GetState user:{userId} pts:{pts} qts:{qts} " +
                       $"seq:{seq} unread:{unreadCount} ///");
            return state;
        }
}
