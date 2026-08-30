// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Stats;

public sealed class StatisticsStore
{
    private readonly IChannelAdminLogRepository _channelAdminLogRepository;
    private readonly IChannelMessagesRepository _channelMessagesRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IMessageInteractionsRepository _messageInteractionsRepository;
    private readonly IMessageReactionsRepository _messageReactionsRepository;
    private readonly IStatisticsRepository _statisticsRepository;

    public const int StatsDcId = 1;

    private readonly IUnitOfWork _unitOfWork;

    public StatisticsStore(IUnitOfWork unitOfWork, IChannelAdminLogRepository channelAdminLogRepository, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IMessageInteractionsRepository messageInteractionsRepository, IMessageReactionsRepository messageReactionsRepository, IStatisticsRepository statisticsRepository)
    {
        _channelAdminLogRepository = channelAdminLogRepository;
        _channelMessagesRepository = channelMessagesRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _messageInteractionsRepository = messageInteractionsRepository;
        _messageReactionsRepository = messageReactionsRepository;
        _statisticsRepository = statisticsRepository;

        _unitOfWork = unitOfWork;
    }

    public async Task<ChannelStatsSnapshot> LoadAsync(long channelId)
    {
        IReadOnlyList<StatsMember> members = await LoadMembersAsync(channelId);
        IReadOnlyList<StatsMessage> messages = await LoadMessagesAsync(channelId);
        IReadOnlyList<StatsView> views = await LoadViewsAsync(channelId, messages);
        IReadOnlyList<StatsReaction> reactions = await LoadReactionsAsync(channelId);
        IReadOnlyList<StatsForward> forwards =
            await LoadForwardsAsync(channelId, messages);
        IReadOnlyList<StatsAdminAction> actions =
            await LoadAdminActionsAsync(channelId);
        return new ChannelStatsSnapshot(members, messages, views, reactions,
            forwards, actions);
    }

    private async Task<IReadOnlyList<StatsMember>> LoadMembersAsync(long channelId)
    {
        IReadOnlyCollection<TLChatParticipantInfo> stored = await _chatParticipantsRepository.GetParticipantsAsync(channelId);
        var members = new List<StatsMember>(stored.Count);
        foreach (TLChatParticipantInfo row in stored)
        {
            using (row)
            {
                var view = row.AsChatParticipantInfo();
                if (view.Role == (int)ChatParticipantRole.Banned ||
                    view.Role == (int)ChatParticipantRole.Left)
                {
                    continue;
                }
                members.Add(new StatsMember(view.UserId, view.InviterId, view.Date));
            }
        }
        return members;
    }

    private async Task<IReadOnlyList<StatsMessage>> LoadMessagesAsync(long channelId)
    {
        IReadOnlyCollection<TLSavedMessage> stored = await _channelMessagesRepository.GetMessagesAsync(channelId);
        var messages = new List<StatsMessage>(stored.Count);
        foreach (TLSavedMessage row in stored)
        {
            using (row)
            {
                MessageView message = row.AsSavedMessage().Get_OriginalMessageView();
                if (!message.Is(out Message body))
                {
                    continue;
                }
                messages.Add(new StatsMessage(body.Id, body.Date,
                    ReadSenderUserId(body), body.MessageProperty.Length));
            }
        }
        return messages;
    }

    private async Task<IReadOnlyList<StatsView>> LoadViewsAsync(long channelId,
        IReadOnlyList<StatsMessage> messages)
    {
        var views = new List<StatsView>();
        foreach (StatsMessage message in messages)
        {
            IReadOnlyCollection<TLMessageViewReceipt> receipts = await _messageInteractionsRepository.GetViewReceiptsAsync(
                    MessageIdentity.ForChannel(channelId, message.Id));
            foreach (TLMessageViewReceipt receipt in receipts)
            {
                using (receipt)
                {
                    var view = receipt.AsMessageViewReceipt();
                    views.Add(new StatsView(message.Id, view.UserId, view.Date));
                }
            }
        }
        return views;
    }

    private async Task<IReadOnlyList<StatsReaction>> LoadReactionsAsync(long channelId)
    {
        IReadOnlyCollection<TLMessageReactionInfo> stored = await _messageReactionsRepository.GetBoxReactionsAsync(
                MessageReactionBox.Channel, channelId);
        var reactions = new List<StatsReaction>();
        foreach (TLMessageReactionInfo row in stored)
        {
            using (row)
            {
                var view = row.AsMessageReactionInfo();
                Vector chosen = view.Reactions;
                int count = chosen.Count;
                for (int i = 0; i < count; i++)
                {
                    var reaction = (ReactionEmoji)chosen.ReadTLObject();
                    if (reaction.Constructor != Constructors.baseLayer_ReactionEmoji)
                    {
                        continue;
                    }
                    reactions.Add(new StatsReaction(view.MessageId, view.Date,
                        Encoding.UTF8.GetString(reaction.Emoticon)));
                }
            }
        }
        return reactions;
    }

    private async Task<IReadOnlyList<StatsForward>> LoadForwardsAsync(long channelId,
        IReadOnlyList<StatsMessage> messages)
    {
        var forwards = new List<StatsForward>();
        foreach (StatsMessage message in messages)
        {
            IReadOnlyCollection<TLPublicForwardRef> stored = await _statisticsRepository.GetPublicForwardsAsync(channelId, message.Id);
            foreach (TLPublicForwardRef row in stored)
            {
                using (row)
                {
                    forwards.Add(new StatsForward(message.Id,
                        row.AsPublicForwardRef().Date));
                }
            }
        }
        return forwards;
    }

    private async Task<IReadOnlyList<StatsAdminAction>> LoadAdminActionsAsync(
        long channelId)
    {
        IReadOnlyCollection<TLAdminLogEvent> stored = await _channelAdminLogRepository.GetEventsAsync(channelId);
        var actions = new List<StatsAdminAction>();
        foreach (TLAdminLogEvent row in stored)
        {
            using (row)
            {
                var view = row.AsAdminLogEvent();
                if (TryReadAdminAction(view.Get_ActionView(),
                        out StatsAdminActionKind kind))
                {
                    actions.Add(new StatsAdminAction(view.UserId, view.Date, kind));
                }
            }
        }
        return actions;
    }

    private static bool TryReadAdminAction(ChannelAdminLogEventActionView action,
        out StatsAdminActionKind kind)
    {
        if (action.Is(out ChannelAdminLogEventActionDeleteMessage _))
        {
            kind = StatsAdminActionKind.Deleted;
            return true;
        }
        if (action.Is(out ChannelAdminLogEventActionParticipantToggleBan ban))
        {
            kind = ban.Get_NewParticipantView()
                .Is(out ChannelParticipantBanned banned) && banned.Left
                ? StatsAdminActionKind.Kicked
                : StatsAdminActionKind.Banned;
            return true;
        }

        kind = default;
        return false;
    }

    private static long ReadSenderUserId(Message message) =>
        message.Flags[8] && message.Get_FromIdView().Is(out PeerUser user)
            ? user.UserId
            : 0;
}
