// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using System.Text.RegularExpressions;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.channels;
using Ferrite.TL.baseLayer.dto;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

public sealed class GetParticipantsHandler : ChannelsHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;

    public GetParticipantsHandler(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository, ICounterFactory counterFactory,
        IdAllocators ids, IUpdatesContextFactory updatesContextFactory,
        IUpdatesService updates, ISearchEngine search, IUploadService upload,
        IPhotoProcessingService photos, ILogger log, ChatRowStore chatRows,
        UpdateFanout fanout)
        : base(unitOfWork, channelMessagesRepository, authorizationRepository, channelAdminLogRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, counterFactory, ids, updatesContextFactory, updates,
            search, upload, photos, log, chatRows, fanout)
    {
        _authorizationRepository = authorizationRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;

    }

    [TLFunction(Constructors.baseLayer_GetParticipants)]
    public async Task<Ferrite.TL.baseLayer.channels.TLChannelParticipants> Handle(
        long authKeyId, TLBytes q)
    {
        var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
        if (auth == null)
        {
            return (Ferrite.TL.baseLayer.channels.TLChannelParticipants)RpcErrorGenerator
                .GenerateError(400, "AUTH_KEY_INVALID"u8);
        }

        long currentUserId = auth.Value.AsAuthInfo().UserId;
        var request = (GetParticipants)q;
        long? channelId = ResolveInputChannelId(request.Get_ChannelView());
        int offset = request.Offset;
        int limit = request.Limit;
        var (filterKind, query) = ReadParticipantsFilter(request.Get_FilterView());
        if (channelId is not > 0)
        {
            return (Ferrite.TL.baseLayer.channels.TLChannelParticipants)RpcErrorGenerator
                .GenerateError(400, "CHANNEL_INVALID"u8);
        }

        using var channel = await _chatRepository.GetChatAsync(channelId.Value);
        if (channel == null || channel.Value.Type != TLChat.ChatType.Channel)
        {
            return (Ferrite.TL.baseLayer.channels.TLChannelParticipants)RpcErrorGenerator
                .GenerateError(400, "CHANNEL_INVALID"u8);
        }

        // Only members see the participant list, and in broadcast channels only the
        // creator/admins do (channelFull.can_view_participants is false for them).
        var caller = await _chatParticipantsRepository
            .GetParticipantAsync(channelId.Value, currentUserId);
        if (caller == null || !IsActiveParticipant(caller.Value))
        {
            caller?.Dispose();
            return (Ferrite.TL.baseLayer.channels.TLChannelParticipants)RpcErrorGenerator
                .GenerateError(400, "CHANNEL_PRIVATE"u8);
        }
        bool callerIsAdmin = ChatRights.HasAdminRight(caller.Value,
            ChatAdminRightRequirement.Any);
        caller.Value.Dispose();
        if (channel.Value.AsChannel().Broadcast && !callerIsAdmin)
        {
            return (Ferrite.TL.baseLayer.channels.TLChannelParticipants)RpcErrorGenerator
                .GenerateError(400, "CHAT_ADMIN_REQUIRED"u8);
        }

        var participantInfos = await _chatParticipantsRepository
            .GetParticipantsAsync(channelId.Value);

        // Filter + order + serialize synchronously (no ref struct crosses an await).
        var selected = new List<(long UserId, int Role, byte[] Bytes)>();
        foreach (var p in participantInfos)
        {
            var info = p.AsChatParticipantInfo();
            if (!MatchesParticipantFilter(info.Role, info.Flags[1], filterKind))
            {
                continue;
            }
            if (query.Length > 0 && !MatchesQuery(info.UserId, query))
            {
                continue;
            }
            selected.Add((info.UserId, info.Role, BuildChannelParticipantBytes(p, currentUserId)));
        }

        selected.Sort((a, b) =>
            a.Role != b.Role ? a.Role.CompareTo(b.Role) : a.UserId.CompareTo(b.UserId));
        int total = selected.Count;
        IEnumerable<(long UserId, int Role, byte[] Bytes)> page = selected.Skip(Math.Max(0, offset));
        if (limit > 0)
        {
            page = page.Take(limit);
        }
        var pageList = page.ToList();

        var participantVector = new Vector();
        foreach (var item in pageList)
        {
            participantVector.AppendTLObject(item.Bytes);
        }
        var userVector = new Vector();
        AppendUsers(ref userVector, pageList.Select(x => x.UserId));

        _log.Debug($"📣 GetParticipants channel:{channelId.Value} filter:{filterKind} " +
                   $"total:{total} page:{pageList.Count}");

        return Ferrite.TL.baseLayer.channels.ChannelParticipants.Builder()
            .Count(total)
            .Participants(participantVector)
            .Chats(new Vector())
            .Users(userVector)
            .Build();
    }
}
