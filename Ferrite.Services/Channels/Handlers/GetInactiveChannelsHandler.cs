// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

/// <summary>
/// The channels the caller belongs to that have gone quiet, which
/// `td_api::getInactiveSupergroupChats` (`Requests.cpp:3268`) offers as
/// leave-candidates when a join hits the membership limit.
///
/// The `dates` vector reports the LAST ACTIVITY date Ferrite genuinely holds --
/// the newest stored message in the channel, or its creation date when it never
/// carried one. Nothing is synthesized: pinned TDLib deliberately ignores this
/// vector anyway ("don't need to use result->dates_, because
/// chat.last_message.date is more reliable", `ChatManager.cpp:1493`), so
/// inventing a plausible number here would be invisible to the client and
/// false in storage.
/// </summary>
public sealed class GetInactiveChannelsHandler : ChannelCatalogueHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;

    // Telegram's own inactivity horizon for this catalogue.
    private static readonly TimeSpan InactivityHorizon = TimeSpan.FromDays(30);

    public GetInactiveChannelsHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,
        ICounterFactory counterFactory, IdAllocators ids,
        IUpdatesContextFactory updatesContextFactory, IUpdatesService updates,
        ISearchEngine search, IUploadService upload,
        IPhotoProcessingService photos, ILogger log, ChatRowStore chatRows,
        UpdateFanout fanout)
        : base(unitOfWork, authorizationRepository, channelAdminLogRepository, channelMessagesRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, counterFactory, ids, updatesContextFactory, updates,
            search, upload, photos, log, chatRows, fanout)
    {
        _authorizationRepository = authorizationRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;

    }

    [TLFunction(Constructors.baseLayer_GetInactiveChannels)]
    public async Task<TLInactiveChats> Handle(long authKeyId, TLBytes q)
    {
        using TLAuthInfo? auth = await _authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        if (auth == null)
        {
            return (TLInactiveChats)RpcErrorGenerator.GenerateError(400,
                "AUTH_KEY_INVALID"u8);
        }

        long callerUserId = auth.Value.AsAuthInfo().UserId;
        int cutoff = (int)DateTimeOffset.Now.Subtract(InactivityHorizon)
            .ToUnixTimeSeconds();
        List<ChannelMembership> membership = await ReadMembershipAsync(callerUserId);
        var selected = new List<(long ChannelId, int Date)>();
        foreach (ChannelMembership channel in membership)
        {
            if (!IsActiveRole(channel.Role))
            {
                continue;
            }

            int lastActivity = await ReadLastActivityDateAsync(channel.ChannelId,
                channel.Date);
            if (lastActivity < cutoff)
            {
                selected.Add((channel.ChannelId, lastActivity));
            }
        }

        // Quietest first: that is the order a client offers leave-candidates in.
        selected.Sort((left, right) => left.Date != right.Date
            ? left.Date.CompareTo(right.Date)
            : left.ChannelId.CompareTo(right.ChannelId));

        // Every row is resolved BEFORE either vector exists: `Vector` and
        // `VectorOfInt` are ref structs and cannot survive an await.
        var rows = new List<(byte[] Row, int Date)>(selected.Count);
        foreach ((long channelId, int date) in selected)
        {
            using TLChat? chat = await _chatRepository
                .GetChatAsync(channelId);
            if (chat is not { Type: TLChat.ChatType.Channel })
            {
                continue;
            }

            rows.Add((await ChannelRows.ForViewerAsync(
                _chatParticipantsRepository, callerUserId, channelId,
                chat.Value.AsSpan().ToArray()), date));
        }

        var chatVector = new Vector();
        var dates = new VectorOfInt();
        foreach ((byte[] row, int date) in rows)
        {
            chatVector.AppendTLObject(row);
            dates.Append(date);
        }

        _log.Debug($"📣 GetInactiveChannels user:{callerUserId} count:{rows.Count}");
        return InactiveChats.Builder()
            .Dates(dates)
            .Chats(chatVector)
            .Users(new Vector())
            .Build();
    }
}
