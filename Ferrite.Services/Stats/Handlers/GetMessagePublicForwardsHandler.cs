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

public sealed class GetMessagePublicForwardsHandler : StatsHandlerBase
{
    private readonly IStatisticsRepository _statisticsRepository;

    private readonly IChannelMessagesRepository _channelMessagesRepository;
    private readonly IChatRepository _chatRepository;

    private const int MaxLimit = 100;

    public GetMessagePublicForwardsHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IStatisticsRepository statisticsRepository, IAuthorizationRepository authorizationRepository, IChannelAdminRepository channelAdminRepository, IChannelMessagesRepository channelMessagesRepository, IChatRepository chatRepository, UserSerializer userSerializer,
        StatisticsStore statistics, StatsGraphTokens tokens, ILogger log)
        : base(unitOfWork, chatParticipantsRepository, authorizationRepository, channelAdminRepository, chatRepository, userSerializer, statistics, tokens, log)
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

        List<ForwardCursor> page = all.Skip(start).Take(limit).ToList();
        var messages = new List<byte[]>(page.Count);
        var channelRows = new List<byte[]>();
        var seenChannels = new HashSet<long>();
        foreach (ForwardCursor entry in page)
        {
            byte[]? messageBytes = await ReadForwardedMessageAsync(entry);
            if (messageBytes == null)
            {
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
        AppendUsers(access.UserId, ref users, messages.Select(ReadSenderUserId).Where(x => x > 0));

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

    private async Task<byte[]?> ReadForwardedMessageAsync(ForwardCursor entry)
    {
        using TLSavedMessage? stored = await _channelMessagesRepository
            .GetMessageAsync(entry.ChannelId, entry.MessageId);
        if (stored == null)
        {
            return null;
        }

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
