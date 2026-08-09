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

public sealed class JoinChannelHandler : ChannelsHandlerBase
{
    private readonly IChatParticipantsRepository _chatParticipantsRepository;

    public JoinChannelHandler(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository, ICounterFactory counterFactory,
        IdAllocators ids, IUpdatesContextFactory updatesContextFactory,
        IUpdatesService updates, ISearchEngine search, IUploadService upload,
        IPhotoProcessingService photos, ILogger log, ChatRowStore chatRows,
        UpdateFanout fanout)
        : base(unitOfWork, channelMessagesRepository, authorizationRepository, channelAdminLogRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, counterFactory, ids, updatesContextFactory, updates,
            search, upload, photos, log, chatRows, fanout)
    {
        _chatParticipantsRepository = chatParticipantsRepository;

    }

    [TLFunction(Constructors.baseLayer_JoinChannel)]
    public async Task<Ferrite.TL.baseLayer.TLUpdates> Handle(long authKeyId, TLBytes q)
    {
        long? channelId = ResolveInputChannelId(((JoinChannel)q).Get_ChannelView());
        var (currentUserId, channelBytes, megagroup, error) =
            await ResolveChannelForMembership(authKeyId, channelId);
        if (error != null)
        {
            return error.Value;
        }

        long id = channelId!.Value;
        var existing = await _chatParticipantsRepository
            .GetParticipantAsync(id, currentUserId);
        bool alreadyActive = existing != null && IsActiveParticipant(existing.Value);
        bool kicked = existing != null &&
            existing.Value.AsChatParticipantInfo().Role == (int)ChatParticipantRole.Banned;
        existing?.Dispose();
        if (kicked)
        {
            return ErrorUpdates("USER_BANNED_IN_CHANNEL"u8);
        }
        if (alreadyActive)
        {
            // Idempotent: the caller is already a member; just echo the channel row.
            return await BuildChannelUpdates(authKeyId, currentUserId, channelBytes,
                Array.Empty<long>());
        }

        int date = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
        using (TLChatParticipantInfo joined = ChatParticipantInfo.Builder()
                   .ChatId(id)
                   .UserId(currentUserId)
                   .Role((int)ChatParticipantRole.Member)
                   .InviterId(currentUserId)
                   .Date(date)
                   .Build())
        {
            _chatParticipantsRepository.PutParticipant(joined);
        }
        byte[] updatedChannelBytes = _chatRows.UpdateStoredChannelParticipantsCount(channelBytes, 1);

        byte[] logAction;
        using (TLChannelAdminLogEventAction action =
               ChannelAdminLogEventActionParticipantJoin.Builder().Build())
        {
            logAction = action.AsSpan().ToArray();
        }
        await AppendAdminLogEventAsync(id, currentUserId, logAction, date,
            ReadUserSearchText(currentUserId));

        _log.Debug($"📣 JoinChannel user:{currentUserId} channel:{id} megagroup:{megagroup}");
        if (megagroup)
        {
            // Megagroups record a join as a messageActionChatAddUser service message.
            byte[] actionBytes;
            {
                var actionUsers = new VectorOfLong();
                actionUsers.Append(currentUserId);
                using TLMessageAction action = MessageActionChatAddUser.Builder()
                    .Users(actionUsers)
                    .Build();
                actionBytes = action.AsSpan().ToArray();
            }
            return await EmitChannelServiceMessage(authKeyId, currentUserId, id,
                updatedChannelBytes, actionBytes);
        }

        return await BuildChannelUpdates(authKeyId, currentUserId, updatedChannelBytes,
            Array.Empty<long>());
    }
}
