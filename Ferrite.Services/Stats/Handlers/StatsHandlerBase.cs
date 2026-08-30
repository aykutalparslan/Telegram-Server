// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.Services.Channels;
using Ferrite.Services.Stats;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.stats;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.StatsMethods;

public abstract class StatsHandlerBase
{
    private readonly IChatParticipantsRepository _chatParticipantsRepository;

    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChannelAdminRepository _channelAdminRepository;
    private readonly IChatRepository _chatRepository;
    private readonly UserSerializer _userSerializer;

    protected readonly IUnitOfWork _unitOfWork;
    protected readonly StatisticsStore _statistics;
    protected readonly StatsGraphTokens _tokens;
    protected readonly ILogger _log;

    protected StatsHandlerBase(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IAuthorizationRepository authorizationRepository, IChannelAdminRepository channelAdminRepository, IChatRepository chatRepository, UserSerializer userSerializer, StatisticsStore statistics,
        StatsGraphTokens tokens, ILogger log)
    {
        _chatParticipantsRepository = chatParticipantsRepository;

        _authorizationRepository = authorizationRepository;
        _channelAdminRepository = channelAdminRepository;
        _chatRepository = chatRepository;
        _userSerializer = userSerializer;

        _unitOfWork = unitOfWork;
        _statistics = statistics;
        _tokens = tokens;
        _log = log;
    }

    protected readonly record struct StatsAccess(long UserId, long ChannelId,
        bool Megagroup, int StatsDc, string? Error)
    {
        public static StatsAccess Failed(string error) =>
            new(0, 0, false, 0, error);
    }

    protected async Task<StatsAccess> AuthorizeAsync(long authKeyId, long? channelId)
    {
        using TLAuthInfo? auth = await _authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        if (auth == null || !auth.Value.AsAuthInfo().LoggedIn)
        {
            return StatsAccess.Failed("AUTH_KEY_INVALID");
        }
        long currentUserId = auth.Value.AsAuthInfo().UserId;

        if (channelId is not > 0)
        {
            return StatsAccess.Failed("CHANNEL_INVALID");
        }

        bool megagroup;
        using (TLChat? chat = await _chatRepository
                   .GetChatAsync(channelId.Value))
        {
            if (chat == null || chat.Value.Type != TLChat.ChatType.Channel)
            {
                return StatsAccess.Failed("CHANNEL_INVALID");
            }
            megagroup = chat.Value.AsChannel().Megagroup;
        }

        using (TLChatParticipantInfo? participant = await _chatParticipantsRepository
                   .GetParticipantAsync(channelId.Value, currentUserId))
        {
            if (participant == null)
            {
                return StatsAccess.Failed("CHAT_ADMIN_REQUIRED");
            }
            if (!ChatRights.HasAdminRight(participant.Value,
                    ChatAdminRightRequirement.Any))
            {
                return StatsAccess.Failed("CHAT_ADMIN_REQUIRED");
            }
        }

        (bool canViewStats, int statsDc) = await ReadStatisticsStateAsync(
            channelId.Value);
        if (!canViewStats || statsDc <= 0)
        {
            return StatsAccess.Failed("CHAT_ADMIN_REQUIRED");
        }

        return new StatsAccess(currentUserId, channelId.Value, megagroup, statsDc,
            null);
    }

    protected async Task<(bool CanViewStats, int StatsDc)> ReadStatisticsStateAsync(
        long channelId)
    {
        TLChannelAdminState? stored = await _channelAdminRepository
            .GetStateAsync(channelId);
        using TLChannelAdminState state = stored ??
            ChannelAdminStateRows.Empty(channelId, 0);
        var view = state.AsChannelAdminState();
        return (view.CanViewStats, view.StatsDc);
    }

    protected void AppendUsers(long viewerUserId, ref Vector userVector, IEnumerable<long> userIds)
    {
        var seen = new HashSet<long>();
        foreach (long userId in userIds)
        {
            if (!seen.Add(userId))
            {
                continue;
            }

            _userSerializer.Append(viewerUserId, ref userVector, userId);
        }
    }

    protected static long? ResolveInputChannelId(InputChannelView channel)
    {
        if (channel.Is(out InputChannel resolved))
        {
            return resolved.ChannelId;
        }
        if (channel.Is(out InputChannelFromMessage fromMessage))
        {
            return fromMessage.ChannelId;
        }
        return null;
    }

    protected static TLStatsGraph Graph(string? json, string? zoomToken)
    {
        if (json == null)
        {
            return GraphError("NOT_ENOUGH_DATA");
        }

        using TLDataJSON data = DataJSON.Builder()
            .Data(Encoding.UTF8.GetBytes(json))
            .Build();
        var builder = StatsGraph.Builder().Json(data.AsSpan());
        if (zoomToken != null)
        {
            builder = builder.ZoomToken(Encoding.UTF8.GetBytes(zoomToken));
        }
        return builder.Build();
    }

    protected static TLStatsGraph GraphError(string message) =>
        StatsGraphError.Builder()
            .Error(Encoding.UTF8.GetBytes(message))
            .Build();

    protected static int UnixNow() => (int)DateTimeOffset.Now.ToUnixTimeSeconds();
}
