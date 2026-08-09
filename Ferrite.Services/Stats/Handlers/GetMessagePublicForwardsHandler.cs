// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Globalization;
using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.Services.Stats;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.stats;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.StatsMethods;

/// <summary>
/// Which public channels re-posted a given channel post.
///
/// The answer comes from the forward index `messages.forwardMessages` writes,
/// which only records a forward whose DESTINATION is a public channel. A forward
/// into a private chat or channel is therefore invisible here — deliberately, as
/// reporting it would tell the source channel's admins about a conversation they
/// were never part of.
///
/// The `offset` is opaque to the client and is this server's own cursor: the sort
/// key of the last row of the previous page. A cursor rather than an index means
/// a forward recorded between two pages cannot shift the second page's contents.
/// </summary>
public sealed class GetMessagePublicForwardsHandler : StatsHandlerBase
{
    private readonly IStatisticsRepository _statisticsRepository;

    private readonly IChannelMessagesRepository _channelMessagesRepository;
    private readonly IChatRepository _chatRepository;

    /// <summary>
    /// The page cap. Pinned TDLib clamps its own request to the same number and
    /// calls it the server-side limit
    /// (`MAX_MESSAGE_FORWARDS`, `StatisticsManager.cpp:899-902`).
    /// </summary>
    private const int MaxLimit = 100;

    public GetMessagePublicForwardsHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IStatisticsRepository statisticsRepository, IAuthorizationRepository authorizationRepository, IChannelAdminRepository channelAdminRepository, IChannelMessagesRepository channelMessagesRepository, IChatRepository chatRepository, IUserRepository userRepository,
        StatisticsStore statistics, StatsGraphTokens tokens, ILogger log)
        : base(unitOfWork, chatParticipantsRepository, authorizationRepository, channelAdminRepository, chatRepository, userRepository, statistics, tokens, log)
    {
        _statisticsRepository = statisticsRepository;

        _channelMessagesRepository = channelMessagesRepository;
        _chatRepository = chatRepository;

    }

    [TLFunction(Constructors.baseLayer_GetMessagePublicForwards)]
    public async Task<TLPublicForwards> Handle(long authKeyId, TLBytes q)
    {
        var request = (GetMessagePublicForwards)q;
        int messageId = request.MsgId;
        int limit = request.Limit;
        string offset = Encoding.UTF8.GetString(request.Offset);
        long? channelId = ResolveInputChannelId(request.Get_ChannelView());

        StatsAccess access = await AuthorizeAsync(authKeyId, channelId);
        if (access.Error != null)
        {
            return Error(access.Error);
        }
        if (limit <= 0)
        {
            return Error("LIMIT_INVALID");
        }
        limit = Math.Min(limit, MaxLimit);

        IReadOnlyCollection<TLPublicForwardRef> stored = await _statisticsRepository
            .GetPublicForwardsAsync(access.ChannelId, messageId);
        var all = new List<ForwardCursor>(stored.Count);
        foreach (TLPublicForwardRef row in stored)
        {
            using (row)
            {
                var view = row.AsPublicForwardRef();
                all.Add(new ForwardCursor(view.Date, view.FwdChannelId,
                    view.FwdMsgId));
            }
        }

        // Newest first, with the channel and message ids breaking ties so the
        // order is total and the cursor can name exactly one row.
        all.Sort(static (left, right) => right.CompareTo(left));
        int start = 0;
        if (offset.Length > 0)
        {
            if (!ForwardCursor.TryParse(offset, out ForwardCursor cursor))
            {
                return Error("OFFSET_INVALID");
            }
            start = all.FindIndex(x => x.CompareTo(cursor) < 0);
            if (start < 0)
            {
                start = all.Count;
            }
        }

        // Every row the page needs is resolved BEFORE a vector exists: `Vector`
        // is a ref struct and cannot be preserved across an await.
        List<ForwardCursor> page = all.Skip(start).Take(limit).ToList();
        var messages = new List<byte[]>(page.Count);
        var channelRows = new List<byte[]>();
        var seenChannels = new HashSet<long>();
        foreach (ForwardCursor entry in page)
        {
            byte[]? messageBytes = await ReadForwardedMessageAsync(entry);
            if (messageBytes == null)
            {
                // The destination copy is gone; pinned TDLib decrements its own
                // total for a forward it cannot materialize, so dropping the row
                // is the same outcome without the wasted round trip.
                continue;
            }
            messages.Add(messageBytes);

            if (!seenChannels.Add(entry.ChannelId))
            {
                continue;
            }
            using TLChat? channel = await _chatRepository
                .GetChatAsync(entry.ChannelId);
            if (channel != null)
            {
                channelRows.Add(channel.Value.AsSpan().ToArray());
            }
        }

        var forwards = new Vector();
        foreach (byte[] messageBytes in messages)
        {
            using TLPublicForward forward = PublicForwardMessage.Builder()
                .Message(messageBytes)
                .Build();
            forwards.AppendTLObject(forward.AsSpan());
        }
        var chats = new Vector();
        foreach (byte[] channelBytes in channelRows)
        {
            chats.AppendTLObject(channelBytes);
        }
        var users = new Vector();
        AppendUsers(ref users, messages.Select(ReadSenderUserId).Where(x => x > 0));

        var builder = PublicForwards.Builder()
            .Count(all.Count)
            .Forwards(forwards)
            .Chats(chats)
            .Users(users);
        if (start + page.Count < all.Count && page.Count > 0)
        {
            builder = builder.NextOffset(
                Encoding.UTF8.GetBytes(page[^1].ToOffset()));
        }

        _log.Debug($"📊 GetMessagePublicForwards user:{access.UserId} " +
                   $"channel:{access.ChannelId} message:{messageId} " +
                   $"page:{page.Count}/{all.Count}");
        return builder.Build();
    }

    /// <summary>
    /// The destination copy of one forward, or null when it no longer exists.
    /// </summary>
    private async Task<byte[]?> ReadForwardedMessageAsync(ForwardCursor entry)
    {
        using TLSavedMessage? stored = await _channelMessagesRepository
            .GetMessageAsync(entry.ChannelId, entry.MessageId);
        if (stored == null)
        {
            return null;
        }

        // Copied out rather than referenced: the stored row is disposed at the
        // end of this frame and the answer is built well after it.
        TLMessage message = stored.Value.AsSavedMessage().Get_OriginalMessage();
        return message.AsSpan().ToArray();
    }

    private static long ReadSenderUserId(byte[] messageBytes)
    {
        using var message = new TLMessage(messageBytes, 0, messageBytes.Length);
        if (message.Type != TLMessage.MessageType.Message)
        {
            return 0;
        }
        var body = message.AsMessage();
        return body.Flags[8] && body.Get_FromIdView().Is(out PeerUser user)
            ? user.UserId
            : 0;
    }

    /// <summary>
    /// One indexed forward and, in its string form, the opaque paging cursor.
    /// Ordering is by recency first so a page reads newest-first.
    /// </summary>
    private readonly record struct ForwardCursor(int Date, long ChannelId,
        int MessageId) : IComparable<ForwardCursor>
    {
        public int CompareTo(ForwardCursor other)
        {
            int result = Date.CompareTo(other.Date);
            if (result != 0) return result;
            result = ChannelId.CompareTo(other.ChannelId);
            return result != 0 ? result : MessageId.CompareTo(other.MessageId);
        }

        public string ToOffset() => string.Create(CultureInfo.InvariantCulture,
            $"{Date}_{ChannelId}_{MessageId}");

        public static bool TryParse(string offset, out ForwardCursor cursor)
        {
            cursor = default;
            string[] parts = offset.Split('_');
            if (parts.Length != 3 ||
                !int.TryParse(parts[0], CultureInfo.InvariantCulture, out int date) ||
                !long.TryParse(parts[1], CultureInfo.InvariantCulture, out long channelId) ||
                !int.TryParse(parts[2], CultureInfo.InvariantCulture, out int messageId))
            {
                return false;
            }
            cursor = new ForwardCursor(date, channelId, messageId);
            return true;
        }
    }

    private static TLPublicForwards Error(string message) =>
        (TLPublicForwards)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));
}
