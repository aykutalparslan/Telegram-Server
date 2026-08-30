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

public sealed class InviteToChannelHandler : ChannelsHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IUserRepository _userRepository;

    public InviteToChannelHandler(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository, ICounterFactory counterFactory,
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
        _userRepository = userRepository;

    }

    [TLFunction(Constructors.layer51_ChannelsInviteToChannel)]
    public async Task<Ferrite.TL.baseLayer.messages.TLInvitedUsers> HandleLayer51(
        long authKeyId, TLBytes q)
    {
        using var current = ToCurrentInviteRequest(q);
        return await Handle(authKeyId, current);
    }

    private static TLBytes ToCurrentInviteRequest(TLBytes q)
    {
        var sent = new TL.layer51.channels.ChannelsInviteToChannel(q.AsSpan());
        using var current = InviteToChannel.Builder()
            .Channel(sent.Channel)
            .Users(sent.Users)
            .Build();
        return current.TLBytes!.Value;
    }

    [TLFunction(Constructors.baseLayer_InviteToChannel)]
    public async Task<Ferrite.TL.baseLayer.messages.TLInvitedUsers> Handle(long authKeyId,
        TLBytes q)
    {
        long? channelId = ResolveInputChannelId(((InviteToChannel)q).Get_ChannelView());
        var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
        if (auth == null)
        {
            return ErrorInvitedUsers("AUTH_KEY_INVALID"u8);
        }

        long currentUserId = auth.Value.AsAuthInfo().UserId;
        if (channelId is not > 0)
        {
            return ErrorInvitedUsers("CHANNEL_INVALID"u8);
        }

        long id = channelId.Value;
        byte[] channelBytes;
        bool megagroup;
        {
            using var channel = await _chatRepository.GetChatAsync(id);
            if (channel == null || channel.Value.Type != TLChat.ChatType.Channel)
            {
                return ErrorInvitedUsers("CHANNEL_INVALID"u8);
            }
            channelBytes = channel.Value.AsSpan().ToArray();
            megagroup = channel.Value.AsChannel().Megagroup;
        }

        int date = (int)DateTimeOffset.Now.ToUnixTimeSeconds();

        var caller = await _chatParticipantsRepository
            .GetParticipantAsync(id, currentUserId);
        if (caller == null || !IsActiveParticipant(caller.Value))
        {
            caller?.Dispose();
            return ErrorInvitedUsers("USER_NOT_PARTICIPANT"u8);
        }
        int callerRole = caller.Value.AsChatParticipantInfo().Role;
        bool allowed = callerRole is (int)ChatParticipantRole.Creator
            or (int)ChatParticipantRole.Admin
            ? ChatRights.HasAdminRight(caller.Value, ChatAdminRightRequirement.InviteUsers)
            : megagroup &&
              !ChatRights.IsRestrictedFrom(caller.Value, ChatBannedAction.InviteUsers, date) &&
              !ChatRights.DefaultBans(channelBytes, ChatBannedAction.InviteUsers);
        caller.Value.Dispose();
        if (!allowed)
        {
            return ErrorInvitedUsers("CHAT_ADMIN_REQUIRED"u8);
        }

        var inviteeIds = ResolveInputUserIds(((InviteToChannel)q).Users, currentUserId);
        var participantInfos = await _chatParticipantsRepository.GetParticipantsAsync(id);
        var activeIds = new HashSet<long>();
        var kickedIds = new HashSet<long>();
        foreach (var p in participantInfos)
        {
            var info = p.AsChatParticipantInfo();
            if (IsActiveParticipant(p))
            {
                activeIds.Add(info.UserId);
            }
            else if (info.Role == (int)ChatParticipantRole.Banned)
            {
                kickedIds.Add(info.UserId);
            }
        }

        var added = new List<long>();
        var missing = new List<long>();
        foreach (long inviteeId in inviteeIds.Distinct())
        {
            if (inviteeId <= 0 || activeIds.Contains(inviteeId))
            {
                continue;
            }
            if (kickedIds.Contains(inviteeId))
            {
                return ErrorInvitedUsers("USER_BANNED_IN_CHANNEL"u8);
            }
            using var user = _userRepository.GetUser(inviteeId);
            if (user == null)
            {
                missing.Add(inviteeId);
                continue;
            }
            using TLChatParticipantInfo participant = ChatParticipantInfo.Builder()
                .ChatId(id)
                .UserId(inviteeId)
                .Role((int)ChatParticipantRole.Member)
                .InviterId(currentUserId)
                .Date(date)
                .Build();
            _chatParticipantsRepository.PutParticipant(participant);
            await AppendInviteEventAsync(id, currentUserId, inviteeId, date);
            added.Add(inviteeId);
        }

        if (added.Count == 0)
        {
            if (inviteeIds.Count == 0)
            {
                return ErrorInvitedUsers("USER_ID_INVALID"u8);
            }
            using var noop = await BuildChannelUpdates(authKeyId, currentUserId, channelBytes,
                Array.Empty<long>());
            var noopMissing = new Vector();
            AppendMissingInvitees(ref noopMissing, missing);
            return Ferrite.TL.baseLayer.messages.InvitedUsers.Builder()
                .Updates(noop.AsSpan())
                .MissingInvitees(noopMissing)
                .Build();
        }

        byte[] updatedChannelBytes = _chatRows.UpdateStoredChannelParticipantsCount(channelBytes, added.Count);

        byte[]? serviceMessageBytes = null;
        int servicePts = 0;
        if (megagroup)
        {
            byte[] actionBytes;
            {
                var actionUsers = new VectorOfLong();
                foreach (long userId in added)
                {
                    actionUsers.Append(userId);
                }
                using TLMessageAction action = MessageActionChatAddUser.Builder()
                    .Users(actionUsers)
                    .Build();
                actionBytes = action.AsSpan().ToArray();
            }
            (serviceMessageBytes, servicePts) =
                await WriteChannelServiceMessage(id, currentUserId, actionBytes, date);
        }

        await _unitOfWork.SaveAsync();

        if (serviceMessageBytes != null)
        {
            var pushTargets = new List<long>(added);
            foreach (long activeId in activeIds)
            {
                if (activeId != currentUserId)
                {
                    pushTargets.Add(activeId);
                }
            }
            foreach (long userId in pushTargets)
            {
                await _fanout.EnqueueNewChannelMessageAsync(userId,
                    serviceMessageBytes, servicePts);
            }
        }
        else
        {
            foreach (long userId in added)
            {
                await _fanout.EnqueueUpdateChannelAsync(userId, id);
            }
        }

        var seqCtx = _updatesContextFactory.GetUpdatesContext(authKeyId, currentUserId);
        int seq = await seqCtx.IncrementSeq();

        var resultUpdates = new Vector();
        using (TLUpdate updateChannel = UpdateChannel.Builder().ChannelId(id).Build())
        {
            resultUpdates.AppendTLObject(updateChannel.AsSpan());
        }
        if (serviceMessageBytes != null)
        {
            using TLUpdate updateNewChannelMessage = UpdateNewChannelMessage.Builder()
                .Message(serviceMessageBytes)
                .Pts(servicePts)
                .PtsCount(1)
                .Build();
            resultUpdates.AppendTLObject(updateNewChannelMessage.AsSpan());
        }

        var userVector = new Vector();
        var resultUserIds = new List<long> { currentUserId };
        resultUserIds.AddRange(added);
        AppendUsers(currentUserId, ref userVector, resultUserIds);
        var chatVector = new Vector();
        chatVector.AppendTLObject(updatedChannelBytes);

        _log.Debug($"📣 InviteToChannel user:{currentUserId} channel:{id} " +
                   $"added:{added.Count} megagroup:{megagroup}");

        using Ferrite.TL.baseLayer.TLUpdates updates = Ferrite.TL.baseLayer.Updates.Builder()
            .UpdatesProperty(resultUpdates)
            .Users(userVector)
            .Chats(chatVector)
            .Date(date)
            .Seq(seq)
            .Build();
        var missingInvitees = new Vector();
        AppendMissingInvitees(ref missingInvitees, missing);
        return Ferrite.TL.baseLayer.messages.InvitedUsers.Builder()
            .Updates(updates.AsSpan())
            .MissingInvitees(missingInvitees)
            .Build();
    }

    private async Task AppendInviteEventAsync(long channelId, long actorUserId,
        long inviteeId, int date)
    {
        byte[] participantBytes;
        using (Ferrite.TL.baseLayer.TLChannelParticipant participant =
               Ferrite.TL.baseLayer.ChannelParticipant.Builder()
                   .UserId(inviteeId)
                   .Date(date)
                   .Build())
        {
            participantBytes = participant.AsSpan().ToArray();
        }

        byte[] logAction;
        using (TLChannelAdminLogEventAction action =
               ChannelAdminLogEventActionParticipantInvite.Builder()
                   .Participant(participantBytes)
                   .Build())
        {
            logAction = action.AsSpan().ToArray();
        }
        await AppendAdminLogEventAsync(channelId, actorUserId, logAction, date,
            ReadUserSearchText(inviteeId));
    }
}
