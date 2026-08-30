// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using System.Text.RegularExpressions;
using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.channels;
using Ferrite.TL.baseLayer.dto;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

public sealed class LeaveChannelHandler : ChannelsHandlerBase
{
    private readonly IChatParticipantsRepository _chatParticipantsRepository;

    public LeaveChannelHandler(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository, ICounterFactory counterFactory,
        IdAllocators ids, IUpdatesContextFactory updatesContextFactory,
        IUpdatesService updates, ISearchEngine search, IUploadService upload,
        IPhotoProcessingService photos, ILogger log, ChatRowStore chatRows,
        UpdateFanout fanout)
        : base(unitOfWork, channelMessagesRepository, authorizationRepository, channelAdminLogRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, counterFactory, ids, updatesContextFactory, updates,
            search, upload, photos, log, chatRows, fanout)
    {
        _chatParticipantsRepository = chatParticipantsRepository;

    }

    [TLFunction(Constructors.baseLayer_LeaveChannel)]
    public async Task<Ferrite.TL.baseLayer.TLUpdates> Handle(long authKeyId, TLBytes q)
    {
        long? channelId = ResolveInputChannelId(((LeaveChannel)q).Get_ChannelView());
        var (currentUserId, channelBytes, megagroup, error) =
            await ResolveChannelForMembership(authKeyId, channelId);
        if (error != null)
        {
            return error.Value;
        }

        long id = channelId!.Value;
        var existing = await _chatParticipantsRepository
            .GetParticipantAsync(id, currentUserId);
        bool active = existing != null && IsActiveParticipant(existing.Value);
        if (!active)
        {
            existing?.Dispose();
            return await BuildChannelUpdates(authKeyId, currentUserId, channelBytes,
                Array.Empty<long>());
        }

        using (TLChatParticipantInfo left = existing!.Value.AsChatParticipantInfo().Clone()
                   .Role((int)ChatParticipantRole.Left)
                   .Build())
        {
            _chatParticipantsRepository.PutParticipant(left);
        }
        existing.Value.Dispose();
        byte[] updatedChannelBytes = _chatRows.UpdateStoredChannelParticipantsCount(channelBytes, -1);

        byte[] logAction;
        using (TLChannelAdminLogEventAction action =
               ChannelAdminLogEventActionParticipantLeave.Builder().Build())
        {
            logAction = action.AsSpan().ToArray();
        }
        await AppendAdminLogEventAsync(id, currentUserId, logAction,
            (int)DateTimeOffset.Now.ToUnixTimeSeconds(),
            ReadUserSearchText(currentUserId));

        _log.Debug($"📣 LeaveChannel user:{currentUserId} channel:{id} megagroup:{megagroup}");
        if (megagroup)
        {
            byte[] actionBytes;
            using (TLMessageAction action = MessageActionChatDeleteUser.Builder()
                       .UserId(currentUserId)
                       .Build())
            {
                actionBytes = action.AsSpan().ToArray();
            }
            return await EmitChannelServiceMessage(authKeyId, currentUserId, id,
                updatedChannelBytes, actionBytes);
        }

        return await BuildChannelUpdates(authKeyId, currentUserId, updatedChannelBytes,
            Array.Empty<long>());
    }
}
