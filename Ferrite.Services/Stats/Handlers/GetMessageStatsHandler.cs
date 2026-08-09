// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.Services.Stats;
using Ferrite.TL;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.stats;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.StatsMethods;

/// <summary>
/// One channel post's statistics: how its views accumulated and which reactions
/// it drew.
///
/// This is the narrowest of the three answers and the only one keyed by a
/// message, so it refuses an id the channel does not hold rather than answering
/// two empty graphs — an empty graph is a statement about a period with no
/// activity, not about a post that does not exist.
/// </summary>
public sealed class GetMessageStatsHandler : StatsHandlerBase
{
    private readonly IChannelMessagesRepository _channelMessagesRepository;

    private static readonly StatsGraphKind[] Graphs =
    [
        StatsGraphKind.MessageViews,
        StatsGraphKind.MessageReactionsByEmotion,
    ];

    public GetMessageStatsHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IAuthorizationRepository authorizationRepository, IChannelAdminRepository channelAdminRepository, IChannelMessagesRepository channelMessagesRepository, IChatRepository chatRepository, IUserRepository userRepository,
        StatisticsStore statistics, StatsGraphTokens tokens, ILogger log)
        : base(unitOfWork, chatParticipantsRepository, authorizationRepository, channelAdminRepository, chatRepository, userRepository, statistics, tokens, log)
    {
        _channelMessagesRepository = channelMessagesRepository;

    }

    [TLFunction(Constructors.baseLayer_GetMessageStats)]
    public async Task<TLMessageStats> Handle(long authKeyId, TLBytes q)
    {
        var request = (GetMessageStats)q;
        bool dark = request.Dark;
        int messageId = request.MsgId;
        long? channelId = ResolveInputChannelId(request.Get_ChannelView());

        StatsAccess access = await AuthorizeAsync(authKeyId, channelId);
        if (access.Error != null)
        {
            return Error(access.Error);
        }

        using (TLSavedMessage? stored = await _channelMessagesRepository
                   .GetMessageAsync(access.ChannelId, messageId))
        {
            if (stored == null)
            {
                return Error("MSG_ID_INVALID");
            }
        }

        int now = UnixNow();
        using StatsGraphSet graphs = _tokens.IssueAll(access.ChannelId, messageId,
            Graphs, dark, now);
        await _unitOfWork.SaveAsync();

        _log.Debug($"📊 GetMessageStats user:{access.UserId} " +
                   $"channel:{access.ChannelId} message:{messageId}");
        return MessageStats.Builder()
            .ViewsGraph(graphs[StatsGraphKind.MessageViews])
            .ReactionsByEmotionGraph(graphs[StatsGraphKind.MessageReactionsByEmotion])
            .Build();
    }

    private static TLMessageStats Error(string message) =>
        (TLMessageStats)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));
}
