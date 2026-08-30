// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.TL;
using Ferrite.TL.baseLayer.channels;
using Ferrite.TL.baseLayer.dto;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

public sealed class GetChannelRecommendationsHandler : ChannelCatalogueHandlerBase
{
    private readonly IChatParticipantsRepository _chatParticipantsRepository;

    private const int MaxRecommendations = 100;

    public GetChannelRecommendationsHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,
        ICounterFactory counterFactory, IdAllocators ids,
        IUpdatesContextFactory updatesContextFactory, IUpdatesService updates,
        ISearchEngine search, IUploadService upload,
        IPhotoProcessingService photos, ILogger log, ChatRowStore chatRows,
        UpdateFanout fanout)
        : base(unitOfWork, authorizationRepository, channelAdminLogRepository, channelMessagesRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, counterFactory, ids, updatesContextFactory, updates,
            search, upload, photos, log, chatRows, fanout)
    {
        _chatParticipantsRepository = chatParticipantsRepository;

    }

    [TLFunction(Constructors.baseLayer_GetChannelRecommendations)]
    public async Task<Ferrite.TL.baseLayer.messages.TLChats> Handle(long authKeyId,
        TLBytes q)
    {
        var request = (GetChannelRecommendations)q;
        long? channelId = request.Flags[0]
            ? ResolveInputChannelId(request.Get_ChannelView())
            : null;

        long? callerUserId = await ResolveCallerAsync(authKeyId);
        if (callerUserId is null)
        {
            return ErrorChats("AUTH_KEY_INVALID"u8);
        }

        List<ChannelMembership> membership = await ReadMembershipAsync(callerUserId.Value);
        var excluded = new HashSet<long>(membership.Select(x => x.ChannelId));
        List<long> seeds;
        if (channelId is not null)
        {
            if (channelId is not > 0)
            {
                return ErrorChats("CHANNEL_INVALID"u8);
            }

            ChannelMembership? subject = await ReadChannelAsync(channelId.Value,
                (int)ChatParticipantRole.Member);
            if (subject is null)
            {
                return ErrorChats("CHANNEL_INVALID"u8);
            }
            if (!subject.Value.Broadcast)
            {
                return await BuildChatsAsync(callerUserId.Value, []);
            }

            seeds = [channelId.Value];
            excluded = [channelId.Value];
        }
        else
        {
            seeds = membership.Where(x => IsActiveRole(x.Role))
                .Select(x => x.ChannelId).ToList();
        }

        Dictionary<long, int> shared = await CountSharedMembershipAsync(
            seeds, excluded, callerUserId.Value);
        var candidates = new List<(long ChannelId, int Shared)>();
        foreach ((long candidateId, int count) in shared)
        {
            ChannelMembership? candidate = await ReadChannelAsync(candidateId,
                (int)ChatParticipantRole.Member);
            if (candidate is { Broadcast: true, HasActiveUsername: true })
            {
                candidates.Add((candidateId, count));
            }
        }

        candidates.Sort((left, right) => left.Shared != right.Shared
            ? right.Shared.CompareTo(left.Shared)
            : left.ChannelId.CompareTo(right.ChannelId));

        List<long> selected = candidates.Take(MaxRecommendations)
            .Select(x => x.ChannelId).ToList();
        _log.Debug($"📣 GetChannelRecommendations user:{callerUserId.Value} " +
                   $"subject:{channelId?.ToString() ?? "self"} count:{selected.Count}");
        return await BuildChatsAsync(callerUserId.Value, selected);
    }

    private async Task<Dictionary<long, int>> CountSharedMembershipAsync(
        IReadOnlyList<long> seeds, HashSet<long> excluded, long callerUserId)
    {
        var counted = new Dictionary<long, int>();
        var seenMembers = new HashSet<long>();
        foreach (long seedId in seeds)
        {
            var participants = await _chatParticipantsRepository
                .GetParticipantsAsync(seedId);
            var memberIds = new List<long>();
            foreach (TLChatParticipantInfo participant in participants)
            {
                using TLChatParticipantInfo owned = participant;
                var info = owned.AsChatParticipantInfo();
                if (info.UserId != callerUserId && IsActiveRole(info.Role) &&
                    seenMembers.Add(info.UserId))
                {
                    memberIds.Add(info.UserId);
                }
            }

            foreach (long memberId in memberIds)
            {
                var theirChannels = await _chatParticipantsRepository
                    .GetParticipantsByUserAsync(memberId);
                foreach (TLChatParticipantInfo participant in theirChannels)
                {
                    using TLChatParticipantInfo owned = participant;
                    var info = owned.AsChatParticipantInfo();
                    if (!IsActiveRole(info.Role) || excluded.Contains(info.ChatId))
                    {
                        continue;
                    }

                    counted[info.ChatId] = counted.GetValueOrDefault(info.ChatId) + 1;
                }
            }
        }

        return counted;
    }
}
