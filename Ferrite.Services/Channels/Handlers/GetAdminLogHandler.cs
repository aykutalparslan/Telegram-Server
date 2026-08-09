// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.Services.Channels;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.channels;
using Ferrite.TL.baseLayer.dto;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

/// <summary>
/// Reads the append-only administrative ledger every channel mutation writes.
///
/// It reports only what Ferrite actually recorded — no event is synthesized at
/// read time — so an administrative action that forgets its append is invisible
/// here, which is what `Phase10hAdminLogTests` gates against.
///
/// Two pinned-client contracts shape the answer and both fail SILENTLY, by
/// dropping the event rather than failing the request. `GetChannelAdminLogQuery`
/// skips any event whose `user_id` is invalid and logs an error for a user it has
/// never seen (`DialogEventLog.cpp:580-585`), so every actor and every
/// participant an action names is carried in `users`. And
/// `get_chat_event_action_object` returns nullptr for an action it cannot map
/// (`DialogEventLog.cpp:42`), which silently removes the event from the client's
/// list, so Ferrite only ever appends actions that mapping accepts.
/// </summary>
public sealed class GetAdminLogHandler : ChannelsHandlerBase
{
    private readonly IChannelAdminLogRepository _channelAdminLogRepository;

    // Every filter bit set: what an absent `events_filter` means. An events_filter
    // that is PRESENT but empty is a different thing and selects nothing, because
    // the flag word IS the selection.
    private const int AllEvents = (1 << 19) - 1;

    // Telegram caps one admin-log page; a client asking for more gets the cap
    // rather than the whole ledger.
    private const int MaxLimit = 100;

    public GetAdminLogHandler(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository, ICounterFactory counterFactory,
        IdAllocators ids, IUpdatesContextFactory updatesContextFactory,
        IUpdatesService updates, ISearchEngine search, IUploadService upload,
        IPhotoProcessingService photos, ILogger log, ChatRowStore chatRows,
        UpdateFanout fanout)
        : base(unitOfWork, channelMessagesRepository, authorizationRepository, channelAdminLogRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, counterFactory, ids, updatesContextFactory, updates,
            search, upload, photos, log, chatRows, fanout)
    {
        _channelAdminLogRepository = channelAdminLogRepository;

    }

    [TLFunction(Constructors.baseLayer_GetAdminLog)]
    public async Task<TLAdminLogResults> Handle(long authKeyId, TLBytes q)
    {
        var request = (GetAdminLog)q;
        long? channelId = ResolveInputChannelId(request.Get_ChannelView());
        string query = ReadQuery(request.Q);
        long maxId = request.MaxId;
        long minId = request.MinId;
        int limit = Math.Min(request.Limit, MaxLimit);
        int filterMask = request.Flags[0]
            ? ChannelAdminLogRows.RequestedMask(
                request.Get_EventsFilterView().AsChannelAdminLogEventsFilter())
            : AllEvents;
        bool hasAdminFilter = request.Flags[1];

        // The admin log is gated on being an administrator, matching
        // `get_dialog_event_log` (`DialogEventLog.cpp:645-647`), which refuses
        // locally before ever sending the query.
        var (currentUserId, channelBytes, error) = await PrepareChannelMutationCore(
            authKeyId, channelId, creatorOnly: false, ChatAdminRightRequirement.Any);
        if (error != null)
        {
            return ErrorAdminLogResults(Encoding.UTF8.GetBytes(error));
        }

        // Re-read after the await rather than before it: `inputUserSelf` in the
        // admins filter can only be resolved once the caller is known, and the
        // request view cannot cross an await.
        HashSet<long>? admins = null;
        if (hasAdminFilter)
        {
            var adminFilter = (GetAdminLog)q;
            admins = ResolveInputUserIds(adminFilter.Admins, currentUserId).ToHashSet();
        }

        IReadOnlyCollection<TLAdminLogEvent> stored = await _channelAdminLogRepository.GetEventsAsync(channelId!.Value);

        var selected = new List<SelectedEvent>();
        var referencedUsers = new List<long>();
        foreach (TLAdminLogEvent row in stored)
        {
            using (row)
            {
                if (!TrySelect(row, query, filterMask, admins, maxId, minId,
                        out SelectedEvent selectedEvent))
                {
                    continue;
                }
                selected.Add(selectedEvent);
            }
        }

        // Newest first, then the page. Ordering is decided here rather than left
        // to storage: the shared KV contract only guarantees prefix iteration.
        selected.Sort(static (left, right) => right.Id.CompareTo(left.Id));
        if (limit > 0 && selected.Count > limit)
        {
            selected.RemoveRange(limit, selected.Count - limit);
        }
        else if (limit <= 0)
        {
            selected.Clear();
        }

        var events = new Vector();
        foreach (SelectedEvent selectedEvent in selected)
        {
            referencedUsers.Add(selectedEvent.UserId);
            referencedUsers.AddRange(selectedEvent.ReferencedUserIds);
            using TLChannelAdminLogEvent wireEvent = ChannelAdminLogEvent.Builder()
                .Id(selectedEvent.Id)
                .Date(selectedEvent.Date)
                .UserId(selectedEvent.UserId)
                .Action(selectedEvent.Action)
                .Build();
            events.AppendTLObject(wireEvent.AsSpan());
        }

        var chats = new Vector();
        chats.AppendTLObject(channelBytes);
        var users = new Vector();
        AppendUsers(ref users, referencedUsers);

        _log.Debug($"📣 GetAdminLog user:{currentUserId} channel:{channelId.Value} events:{selected.Count}");
        return AdminLogResults.Builder()
            .Events(events)
            .Chats(chats)
            .Users(users)
            .Build();
    }

    private readonly record struct SelectedEvent(long Id, int Date, long UserId,
        byte[] Action, IReadOnlyList<long> ReferencedUserIds);

    // One synchronous frame over the stored row: nothing here may outlive the
    // buffer, so everything the page needs is copied out before returning.
    private static bool TrySelect(TLAdminLogEvent row, string query, int filterMask,
        HashSet<long>? admins, long maxId, long minId, out SelectedEvent selected)
    {
        selected = default;
        var view = row.AsAdminLogEvent();
        long id = view.Id;

        // max_id/min_id are exclusive bounds around the page, and 0 means
        // unbounded on that side: pinned TDLib pages backwards from the newest
        // event with `from_event_id` in max_id and always sends min_id 0
        // (`DialogEventLog.cpp:556-557`).
        if (maxId > 0 && id >= maxId)
        {
            return false;
        }
        if (minId > 0 && id <= minId)
        {
            return false;
        }
        if (admins != null && !admins.Contains(view.UserId))
        {
            return false;
        }

        ChannelAdminLogEventActionView action = view.Get_ActionView();
        if ((ChannelAdminLogRows.FilterMask(action.Constructor) & filterMask) == 0)
        {
            return false;
        }
        if (query.Length > 0 && !MatchesSearchText(view.SearchText, query))
        {
            return false;
        }

        selected = new SelectedEvent(id, view.Date, view.UserId,
            view.Get_Action().AsSpan().ToArray(), ReadReferencedUserIds(action));
        return true;
    }

    private static bool MatchesSearchText(ReadOnlySpan<byte> searchText, string query) =>
        searchText.Length > 0 &&
        Encoding.UTF8.GetString(searchText)
            .Contains(query, StringComparison.OrdinalIgnoreCase);

    // The users an action names besides its actor. Pinned TDLib resolves these
    // through UserManager and logs an error for one it has never seen, so they
    // travel with the page.
    private static IReadOnlyList<long> ReadReferencedUserIds(
        ChannelAdminLogEventActionView action)
    {
        if (action.Is(out ChannelAdminLogEventActionParticipantToggleBan ban))
        {
            return Referenced(ReadParticipantUserId(ban.Get_NewParticipantView()),
                ReadParticipantUserId(ban.Get_PrevParticipantView()));
        }
        if (action.Is(out ChannelAdminLogEventActionParticipantToggleAdmin admin))
        {
            return Referenced(ReadParticipantUserId(admin.Get_NewParticipantView()),
                ReadParticipantUserId(admin.Get_PrevParticipantView()));
        }
        if (action.Is(out ChannelAdminLogEventActionParticipantInvite invite))
        {
            return Referenced(ReadParticipantUserId(invite.Get_ParticipantView()), 0);
        }

        return Array.Empty<long>();

        static IReadOnlyList<long> Referenced(long first, long second) =>
            second is > 0 && second != first ? [first, second] : [first];
    }

    private static long ReadParticipantUserId(
        Ferrite.TL.baseLayer.ChannelParticipantView participant)
    {
        if (participant.Is(out ChannelParticipant member))
        {
            return member.UserId;
        }
        if (participant.Is(out ChannelParticipantSelf self))
        {
            return self.UserId;
        }
        if (participant.Is(out ChannelParticipantCreator creator))
        {
            return creator.UserId;
        }
        if (participant.Is(out ChannelParticipantAdmin admin))
        {
            return admin.UserId;
        }
        if (participant.Is(out ChannelParticipantBanned banned))
        {
            return banned.Get_PeerView().Is(out PeerUser bannedUser)
                ? bannedUser.UserId : 0;
        }
        if (participant.Is(out ChannelParticipantLeft left))
        {
            return left.Get_PeerView().Is(out PeerUser leftUser) ? leftUser.UserId : 0;
        }

        return 0;
    }

    private static TLAdminLogResults ErrorAdminLogResults(ReadOnlySpan<byte> message) =>
        (TLAdminLogResults)RpcErrorGenerator.GenerateError(400, message);
}
