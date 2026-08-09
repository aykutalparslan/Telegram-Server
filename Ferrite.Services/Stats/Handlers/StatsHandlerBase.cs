// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Services.Channels;
using Ferrite.Services.Stats;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.StatsMethods;

/// <summary>
/// The access check every `stats.*` method shares, plus the snapshot load.
///
/// Statistics are ADMIN-ONLY, which is also what pinned TDLib assumes: it gates
/// its statistics screen on `channelFull.can_view_stats`, and Ferrite only sets
/// that flag for a caller who passes the check here. The server still performs
/// the check itself — a client is free to send the query regardless of what its
/// own UI would allow.
/// </summary>
public abstract class StatsHandlerBase
{
    private readonly IChatParticipantsRepository _chatParticipantsRepository;

    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChannelAdminRepository _channelAdminRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IUserRepository _userRepository;

    protected readonly IUnitOfWork _unitOfWork;
    protected readonly StatisticsStore _statistics;
    protected readonly StatsGraphTokens _tokens;
    protected readonly ILogger _log;

    protected StatsHandlerBase(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IAuthorizationRepository authorizationRepository, IChannelAdminRepository channelAdminRepository, IChatRepository chatRepository, IUserRepository userRepository, StatisticsStore statistics,
        StatsGraphTokens tokens, ILogger log)
    {
        _chatParticipantsRepository = chatParticipantsRepository;

        _authorizationRepository = authorizationRepository;
        _channelAdminRepository = channelAdminRepository;
        _chatRepository = chatRepository;
        _userRepository = userRepository;

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

    /// <summary>
    /// Resolves the channel, proves the caller may read its statistics, and
    /// reports the DC the statistics are served from.
    ///
    /// The stored `dto.channelAdminState` is the channel's own record of whether
    /// its statistics are servable and from where; the caller's administrator
    /// status is checked on top of it. Both must hold, which is exactly the pair
    /// `channelFull` reports as `stats_dc` + `can_view_stats`.
    /// </summary>
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

    /// <summary>
    /// The channel's stored statistics availability, defaulting to what a
    /// channel with no administration row behaves as.
    /// </summary>
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

    /// <summary>
    /// Every user the answer names, each once. Pinned TDLib resolves the user of
    /// a top-poster/admin/inviter row through UserManager and DROPS the row when
    /// it has never seen that user, so a name missing here silently shortens the
    /// client's list rather than failing the request.
    /// </summary>
    protected void AppendUsers(ref Vector userVector, IEnumerable<long> userIds)
    {
        var seen = new HashSet<long>();
        foreach (long userId in userIds)
        {
            if (!seen.Add(userId))
            {
                continue;
            }

            using TLUser? user = _userRepository.GetUser(userId);
            if (user != null)
            {
                userVector.AppendTLObject(user.Value.AsSpan());
            }
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

    protected static int UnixNow() => (int)DateTimeOffset.Now.ToUnixTimeSeconds();
}
