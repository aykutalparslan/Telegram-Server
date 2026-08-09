// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.updates;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.UpdateMethods;

public abstract class UpdatesHandlerBase
{
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;

    protected readonly IMTProtoTime _time;
    protected readonly IUnitOfWork _unitOfWork;
    protected readonly IUpdatesContextFactory _updatesContextFactory;
    protected readonly ICounterFactory _counterFactory;
    protected readonly ILogger _log;

    protected UpdatesHandlerBase(IMTProtoTime time, IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository,
        IUpdatesContextFactory updatesContextFactory, ICounterFactory counterFactory,
        ILogger log)
    {
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;

        _time = time;
        _unitOfWork = unitOfWork;
        _updatesContextFactory = updatesContextFactory;
        _counterFactory = counterFactory;
        _log = log;
    }

    protected async Task<TLState> GetStateInternal(IUpdatesContext updatesCtx)
    {
        int date = (int)_time.GetUnixTimeInSeconds();
        int qts = await updatesCtx.Qts();
        return await GetStateInternal(updatesCtx, date, qts);
    }

    protected static async Task<TLState> GetStateInternal(IUpdatesContext updatesCtx,
        int date, int qts)
    {
        int pts = await updatesCtx.Pts();
        int seq = await updatesCtx.Seq();
        int unreadCount = await updatesCtx.UnreadMessages();
        return State.Builder()
            .Date(date)
            .Pts(pts)
            .Seq(seq)
            .Qts(qts)
            .UnreadCount(unreadCount)
            .Build();
    }

    protected async Task<List<(long ChannelId, int Pts, byte[] ChannelBytes)>>
        GatherChannelTooLong(long userId)
    {
        var markers = new List<(long, int, byte[])>();
        var participations = await _chatParticipantsRepository
            .GetParticipantsByUserAsync(userId);

        foreach (var participation in participations)
        {
            if (!IsActiveParticipant(participation)) continue;
            long chatId = participation.AsChatParticipantInfo().ChatId;
            using var chat = await _chatRepository.GetChatAsync(chatId);
            if (chat == null || chat.Value.Type != TLChat.ChatType.Channel) continue;

            var channelBox = new ChannelMessageBox(_counterFactory, chatId);
            int channelPts = await channelBox.Pts();
            if (channelPts <= 1) continue;
            markers.Add((chatId, channelPts, chat.Value.AsSpan().ToArray()));
        }

        return markers;
    }

    private static bool IsActiveParticipant(TLChatParticipantInfo participant)
    {
        int role = participant.AsChatParticipantInfo().Role;
        return role != (int)ChatParticipantRole.Banned &&
               role != (int)ChatParticipantRole.Left;
    }
}
