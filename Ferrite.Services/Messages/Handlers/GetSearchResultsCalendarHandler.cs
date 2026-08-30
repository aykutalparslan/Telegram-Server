// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class GetSearchResultsCalendarHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly MessageSearchService _search;
    private readonly DialogBuilder _dialogs;
    private readonly UpdateFanout _fanout;

    public GetSearchResultsCalendarHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository,
        MessageSearchService search, DialogBuilder dialogs, UpdateFanout fanout)
    {
        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
        _search = search;
        _dialogs = dialogs;
        _fanout = fanout;
    }

    private sealed class Period
    {
        public int Date;
        public int MinMsgId;
        public int MaxMsgId;
        public int Count;
        public byte[] Opening = [];
    }

    [TLFunction(Constructors.baseLayer_GetSearchResultsCalendar)]
    public async Task<TLSearchResultsCalendar> Handle(long authKeyId, TLBytes q)
    {
        long userId;
        using (TLAuthInfo? auth = await _authorizationRepository
                   .GetAuthorizationAsync(authKeyId))
        {
            if (auth == null || !auth.Value.AsAuthInfo().LoggedIn)
            {
                return (TLSearchResultsCalendar)RpcErrorGenerator.GenerateError(401,
                    "AUTH_KEY_INVALID"u8);
            }
            userId = auth.Value.AsAuthInfo().UserId;
        }

        var request = (GetSearchResultsCalendar)q;
        MessageSearchTarget target = request.Flags[2]
            ? MessageSearchService.ResolveTarget(request.Get_SavedPeerIdView(),
                userId)
            : MessageSearchService.ResolveTarget(request.Get_PeerView(), userId);
        (TLMessagesFilter.MessagesFilterType filter, bool missedOnly) =
            MessageSearchService.ReadFilter(request.Get_FilterView());
        MessageSearchFilter.Criteria criteria = new MessageSearchFilter.Criteria
        {
            Filter = filter,
            MissedCallsOnly = missedOnly,
            ViewerUserId = userId,
        };
        int offsetId = request.OffsetId;
        int offsetDate = request.OffsetDate;

        (string? error, List<MessageSnapshot> matched) = await _search.SelectPeerAsync(
            userId, target, criteria);
        if (error != null)
        {
            return (TLSearchResultsCalendar)RpcErrorGenerator.GenerateError(400,
                Encoding.UTF8.GetBytes(error));
        }

        List<MessageSnapshot> page = SelectPage(matched, offsetId, offsetDate);
        List<Period> periods = GroupByMonth(page);
        var opening = periods.Select(x => x.Opening).ToList();
        (HashSet<long> relatedUserIds, List<byte[]> relatedChatBytes) = await _dialogs
            .ResolveRelatedPeersAsync(userId, target.PeerType, target.PeerId, opening);

        return Build(userId, matched.Count, matched.Count - page.Count, page, periods,
            relatedUserIds, relatedChatBytes);
    }

    private static List<MessageSnapshot> SelectPage(
        IReadOnlyList<MessageSnapshot> matched, int offsetId, int offsetDate)
    {
        if (offsetId <= 0 && offsetDate <= 0)
        {
            return [.. matched];
        }
        var page = new List<MessageSnapshot>();
        foreach (MessageSnapshot snapshot in matched)
        {
            if (offsetId > 0 && snapshot.Id >= offsetId)
            {
                continue;
            }
            if (offsetDate > 0 && snapshot.Date > offsetDate)
            {
                continue;
            }
            page.Add(snapshot);
        }
        return page;
    }

    private static List<Period> GroupByMonth(IReadOnlyList<MessageSnapshot> page)
    {
        var periods = new List<Period>();
        var byMonth = new Dictionary<int, Period>();
        foreach (MessageSnapshot snapshot in page)
        {
            int month = MonthStart(snapshot.Date);
            if (!byMonth.TryGetValue(month, out Period? period))
            {
                period = new Period
                {
                    Date = month,
                    MinMsgId = snapshot.Id,
                    MaxMsgId = snapshot.Id,
                    Opening = snapshot.Bytes,
                };
                byMonth[month] = period;
                periods.Add(period);
            }

            period.Count++;
            if (snapshot.Id > period.MaxMsgId)
            {
                period.MaxMsgId = snapshot.Id;
            }
            if (snapshot.Id <= period.MinMsgId)
            {
                period.MinMsgId = snapshot.Id;
                period.Opening = snapshot.Bytes;
            }
        }
        return periods;
    }

    private static int MonthStart(int date)
    {
        DateTimeOffset moment = DateTimeOffset.FromUnixTimeSeconds(date);
        return (int)new DateTimeOffset(moment.Year, moment.Month, 1, 0, 0, 0,
            TimeSpan.Zero).ToUnixTimeSeconds();
    }

    private TLSearchResultsCalendar Build(long viewerUserId, int totalCount, int offsetIdOffset,
        IReadOnlyList<MessageSnapshot> page, List<Period> periods,
        IEnumerable<long> userIds, IReadOnlyCollection<byte[]> chatBytes)
    {
        var periodVector = new Vector();
        var messages = new Vector();
        foreach (Period period in periods)
        {
            using TLSearchResultsCalendarPeriod row = SearchResultsCalendarPeriod
                .Builder()
                .Date(period.Date)
                .MinMsgId(period.MinMsgId)
                .MaxMsgId(period.MaxMsgId)
                .Count(period.Count)
                .Build();
            periodVector.AppendTLObject(row.AsSpan());
            messages.AppendTLObject(period.Opening);
        }

        var users = new Vector();
        _fanout.AppendUsers(viewerUserId, ref users, userIds);
        var chats = new Vector();
        foreach (byte[] chat in chatBytes)
        {
            chats.AppendTLObject(chat);
        }

        MessageSnapshot? oldest = page.Count > 0 ? page[^1] : null;
        return SearchResultsCalendar.Builder()
            .Count(totalCount)
            .MinDate(oldest?.Date ?? 0)
            .MinMsgId(oldest?.Id ?? 0)
            .OffsetIdOffset(offsetIdOffset)
            .Periods(periodVector)
            .Messages(messages)
            .Chats(chats)
            .Users(users)
            .Build();
    }
}
