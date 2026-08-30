// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.MessageMethods;

public abstract class MessagesHandlerBase
{
    private readonly IForumTopicsRepository _forumTopicsRepository;
    private readonly IMessagingSettingsRepository _messagingSettingsRepository;

    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChannelMessagesRepository _channelMessagesRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IUserRepository _userRepository;

    protected readonly IUnitOfWork _unitOfWork;
    protected readonly ISearchEngine _search;
    protected readonly IUpdatesService _updates;
    protected readonly IUpdatesContextFactory _updatesContextFactory;
    protected readonly ILogger _log;
    protected readonly IUploadService _upload;
    protected readonly IPhotoProcessingService _photos;
    protected readonly ICounterFactory _counterFactory;
    protected readonly ChatRowStore _chatRows;
    protected readonly InviteStore _invites;
    protected readonly PrivacyEvaluator _privacy;
    protected readonly IdAllocators _ids;
    protected readonly MessageStore _messages;
    protected readonly SendPipeline _send;
    protected readonly UpdateFanout _fanout;
    protected readonly DialogBuilder _dialogs;

    protected MessagesHandlerBase(IUnitOfWork unitOfWork, IForumTopicsRepository forumTopicsRepository, IMessagingSettingsRepository messagingSettingsRepository, IAuthorizationRepository authorizationRepository, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,ISearchEngine search,
        IUpdatesService updates, IUpdatesContextFactory updatesContextFactory,
        ILogger log, IUploadService upload, IPhotoProcessingService photos,
        ICounterFactory counterFactory, IdAllocators ids,
        ChatRowStore chatRows, InviteStore invites,
        PrivacyEvaluator privacy, MessageStore messages, SendPipeline send,
        UpdateFanout fanout, DialogBuilder dialogs)
    {
        _forumTopicsRepository = forumTopicsRepository;
        _messagingSettingsRepository = messagingSettingsRepository;

        _authorizationRepository = authorizationRepository;
        _channelMessagesRepository = channelMessagesRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;
        _messageRepository = messageRepository;
        _userRepository = userRepository;

        _unitOfWork = unitOfWork;
        _search = search;
        _updates = updates;
        _updatesContextFactory = updatesContextFactory;
        _log = log;
        _upload = upload;
        _photos = photos;
        _counterFactory = counterFactory;
        _ids = ids;
        _chatRows = chatRows;
        _invites = invites;
        _privacy = privacy;
        _messages = messages;
        _send = send;
        _fanout = fanout;
        _dialogs = dialogs;
    }

    protected async ValueTask<TLBool> EditChannelAbout(long authKeyId, long channelId, byte[] about)
    {
        var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
        if (auth == null)
        {
            return ErrorBool("AUTH_KEY_INVALID");
        }

        long currentUserId = auth.Value.AsAuthInfo().UserId;
        using var channel = await _chatRepository.GetChatAsync(channelId);
        if (channel == null || channel.Value.Type != TLChat.ChatType.Channel)
        {
            return ErrorBool("CHANNEL_INVALID");
        }

        var participant = await _chatParticipantsRepository
            .GetParticipantAsync(channelId, currentUserId);
        if (participant == null || !IsActiveParticipant(participant.Value))
        {
            participant?.Dispose();
            return ErrorBool("USER_NOT_PARTICIPANT");
        }

        bool canChangeInfo = ChatRights.HasAdminRight(participant.Value,
            ChatAdminRightRequirement.ChangeInfo);
        participant.Value.Dispose();
        if (!canChangeInfo)
        {
            return ErrorBool("CHAT_ADMIN_REQUIRED");
        }

        await PutChatAbout(channelId, about);
        await _unitOfWork.SaveAsync();
        _log.Debug($"📣 EditChatAbout(channel) user:{currentUserId} channel:{channelId}");
        return BoolTrue.Builder().Build();
    }

    protected async Task<TLUpdates> EditChatDefaultBannedRightsForChat(long authKeyId,
        long chatId, byte[] rightsBytes)
    {
        var (context, error) = await PrepareBasicChatMutation(authKeyId, chatId,
            requireAdmin: true);
        if (error != null)
        {
            return ErrorUpdates(error);
        }

        try
        {
            byte[] updatedChatBytes =
                _chatRows.UpdateStoredChatDefaultBannedRights(context.ChatBytes, rightsBytes);
            int newVersion = ReadChatVersion(updatedChatBytes);
            await _unitOfWork.SaveAsync();

            byte[] updateBytes;
            using (TLPeer chatPeer = new PeerChat(chatId))
            using (TLUpdate update = UpdateChatDefaultBannedRights.Builder()
                       .Peer(chatPeer.AsSpan())
                       .DefaultBannedRights(rightsBytes)
                       .Version(newVersion)
                       .Build())
            {
                updateBytes = update.AsSpan().ToArray();
            }
            foreach (var participantInfo in context.ActiveParticipants)
            {
                long participantId = participantInfo.AsChatParticipantInfo().UserId;
                if (participantId != context.CurrentUserId)
                {
                    await _updates.EnqueueUpdate(participantId,
                        new TLUpdate(updateBytes, 0, updateBytes.Length));
                }
            }

            _log.Debug($"👥 EditChatDefaultBannedRights user:{context.CurrentUserId} " +
                       $"chat:{chatId} version:{newVersion}");
            return await BuildDefaultBannedRightsResult(authKeyId, context.CurrentUserId,
                updatedChatBytes, updateBytes);
        }
        finally
        {
            DisposeParticipants(context.ActiveParticipants);
        }
    }

    protected async Task<TLUpdates> EditChatDefaultBannedRightsForChannel(long authKeyId,
        long channelId, byte[] rightsBytes)
    {
        var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
        if (auth == null)
        {
            return ErrorUpdates("AUTH_KEY_INVALID");
        }

        long currentUserId = auth.Value.AsAuthInfo().UserId;
        byte[] channelBytes;
        {
            using var channel = await _chatRepository.GetChatAsync(channelId);
            if (channel == null || channel.Value.Type != TLChat.ChatType.Channel)
            {
                return ErrorUpdates("CHANNEL_INVALID");
            }
            channelBytes = channel.Value.AsSpan().ToArray();
        }

        var participant = await _chatParticipantsRepository
            .GetParticipantAsync(channelId, currentUserId);
        if (participant == null || !IsActiveParticipant(participant.Value))
        {
            participant?.Dispose();
            return ErrorUpdates("USER_NOT_PARTICIPANT");
        }
        bool canBan = ChatRights.HasAdminRight(participant.Value,
            ChatAdminRightRequirement.BanUsers);
        participant.Value.Dispose();
        if (!canBan)
        {
            return ErrorUpdates("CHAT_ADMIN_REQUIRED");
        }

        byte[] updatedChannelBytes =
            _chatRows.UpdateStoredChannelDefaultBannedRights(channelBytes, rightsBytes);
        await _unitOfWork.SaveAsync();

        byte[] updateBytes;
        using (TLPeer channelPeer = new PeerChannel(channelId))
        using (TLUpdate update = UpdateChatDefaultBannedRights.Builder()
                   .Peer(channelPeer.AsSpan())
                   .DefaultBannedRights(rightsBytes)
                   .Version(0)
                   .Build())
        {
            updateBytes = update.AsSpan().ToArray();
        }
        var memberIds = await _fanout.GetOtherActiveChannelMemberIdsAsync(channelId,
            currentUserId);
        foreach (long memberId in memberIds)
        {
            await _updates.EnqueueUpdate(memberId,
                new TLUpdate(updateBytes, 0, updateBytes.Length));
        }

        _log.Debug($"📣 EditChatDefaultBannedRights user:{currentUserId} " +
                   $"channel:{channelId} members:{memberIds.Count}");
        return await BuildDefaultBannedRightsResult(authKeyId, currentUserId,
            updatedChannelBytes, updateBytes);
    }

    protected async Task<TLUpdates> BuildDefaultBannedRightsResult(long authKeyId,
        long actorUserId, byte[] chatBytes, byte[] updateBytes)
    {
        var seqCtx = _updatesContextFactory.GetUpdatesContext(authKeyId, actorUserId);
        int seq = await seqCtx.IncrementSeq();

        var resultUpdates = new Vector();
        resultUpdates.AppendTLObject(updateBytes);
        var userVector = new Vector();
        AppendUsers(actorUserId, ref userVector, new[] { actorUserId });
        var chatVector = new Vector();
        chatVector.AppendTLObject(chatBytes);

        return Ferrite.TL.baseLayer.Updates.Builder()
            .UpdatesProperty(resultUpdates)
            .Users(userVector)
            .Chats(chatVector)
            .Date((int)DateTimeOffset.Now.ToUnixTimeSeconds())
            .Seq(seq)
            .Build();
    }

    protected async Task CopyRecentChatHistory(long sourceUserId, long targetUserId,
        long chatId, int fwdLimit)
    {
        const int maxForwardLimit = 100;
        int limit = Math.Clamp(fwdLimit, 0, maxForwardLimit);
        if (limit == 0)
        {
            return;
        }

        var saved = await _messageRepository.GetMessagesAsync(sourceUserId);
        var history = new List<(int Id, byte[] Bytes)>();
        foreach (var s in saved)
        {
            using var savedMessage = s;
            var message = savedMessage.AsSavedMessage().Get_OriginalMessage();
            if (message.Type != TLMessage.MessageType.Message ||
                !MessageStore.TryReadStoredMessageInfo(message, out var info) ||
                info.PeerType != TLPeer.PeerType.PeerChat ||
                info.PeerId != chatId)
            {
                continue;
            }
            history.Add((info.Id, info.Bytes));
        }

        var toCopy = history
            .OrderByDescending(m => m.Id)
            .Take(limit)
            .Reverse()
            .ToList();
        var targetCtx = _updatesContextFactory.GetUpdatesContext(null, targetUserId);
        foreach (var (_, bytes) in toCopy)
        {
            int newId = (int)await targetCtx.NextMessageId();
            int pts = await targetCtx.IncrementPtsForMessage(
                (int)TLPeer.PeerType.PeerChat, chatId, newId);
            using var original = new TLMessage(bytes, 0, bytes.Length);
            using TLMessage copy = original.AsMessage().Clone()
                .Id(newId)
                .OutProperty(false)
                .Build();
            _messageRepository.PutMessage(targetUserId, copy, pts);
        }
    }

    protected TLInvitedUsers BuildPrivacyBlockedInvitedUsers(long viewerUserId, long targetUserId,
        byte[] chatBytes, int date)
    {
        var userVector = new Vector();
        AppendUsers(viewerUserId, ref userVector, new[] { targetUserId });
        var chatVector = new Vector();
        chatVector.AppendTLObject(chatBytes);
        using TLUpdates updates = Ferrite.TL.baseLayer.Updates.Builder()
            .UpdatesProperty(new Vector())
            .Users(userVector)
            .Chats(chatVector)
            .Date(date)
            .Seq(0)
            .Build();

        var missingInvitees = new Vector();
        using (var missingInvitee = MissingInvitee.Builder()
                   .UserId(targetUserId)
                   .Build())
        {
            missingInvitees.AppendTLObject(missingInvitee.ToReadOnlySpan());
        }

        return InvitedUsers.Builder()
            .Updates(updates.AsSpan())
            .MissingInvitees(missingInvitees)
            .Build();
    }

    protected async Task<TLUpdates> ImportBasicChatInvite(long authKeyId, long joinerUserId,
        long chatId, long inviterAdminId, byte[] chatBytes, int date)
    {
        var allParticipants = await _chatParticipantsRepository
            .GetParticipantsAsync(chatId);
        var activeParticipants = new List<TLChatParticipantInfo>();
        foreach (var participantInfo in allParticipants)
        {
            if (IsActiveParticipant(participantInfo))
            {
                activeParticipants.Add(participantInfo);
            }
            else
            {
                participantInfo.Dispose();
            }
        }

        TLChatParticipantInfo joined = ChatParticipantInfo.Builder()
            .ChatId(chatId)
            .UserId(joinerUserId)
            .Role((int)ChatParticipantRole.Member)
            .InviterId(inviterAdminId)
            .Date(date)
            .Build();
        try
        {
            _chatParticipantsRepository.PutParticipant(joined);
            byte[] updatedChatBytes = _chatRows.UpdateStoredChatMembership(chatBytes, 1);
            int newVersion = ReadChatVersion(updatedChatBytes);

            var fanoutParticipants = new List<TLChatParticipantInfo>(activeParticipants)
            {
                joined
            };
            byte[] actionBytes;
            using (TLMessageAction action = MessageActionChatJoinedByLink.Builder()
                       .InviterId(inviterAdminId)
                       .Build())
            {
                actionBytes = action.AsSpan().ToArray();
            }

            byte[] participantsUpdateBytes =
                BuildChatParticipantsUpdateBytes(chatId, fanoutParticipants, newVersion);
            return await EmitBasicGroupServiceUpdates(authKeyId, joinerUserId, chatId,
                fanoutParticipants, actionBytes, updatedChatBytes, participantsUpdateBytes);
        }
        finally
        {
            joined.Dispose();
            DisposeParticipants(activeParticipants);
        }
    }

    protected async Task<TLUpdates> ImportChannelInvite(long authKeyId, long joinerUserId,
        long channelId, long inviterAdminId, byte[] channelBytes, int date)
    {
        bool megagroup;
        {
            using var stored = new TLChat(channelBytes, 0, channelBytes.Length);
            megagroup = stored.AsChannel().Megagroup;
        }

        using (TLChatParticipantInfo joined = ChatParticipantInfo.Builder()
                   .ChatId(channelId)
                   .UserId(joinerUserId)
                   .Role((int)ChatParticipantRole.Member)
                   .InviterId(inviterAdminId)
                   .Date(date)
                   .Build())
        {
            _chatParticipantsRepository.PutParticipant(joined);
        }
        byte[] updatedChannelBytes = _chatRows.UpdateStoredChannelMembership(channelBytes, 1);

        byte[]? serviceMessageBytes = null;
        int servicePts = 0;
        if (megagroup)
        {
            byte[] actionBytes;
            using (TLMessageAction action = MessageActionChatJoinedByLink.Builder()
                       .InviterId(inviterAdminId)
                       .Build())
            {
                actionBytes = action.AsSpan().ToArray();
            }
            var channelBox = new ChannelMessageBox(_counterFactory, channelId);
            int messageId = await channelBox.NextMessageId();
            using TLPeer channelPeer = new PeerChannel(channelId);
            using TLPeer joinerPeer = new PeerUser(joinerUserId);
            using TLMessage serviceMessage = MessageService.Builder()
                .Id(messageId)
                .FromId(joinerPeer.AsSpan())
                .PeerId(channelPeer.AsSpan())
                .Date(date)
                .Action(actionBytes)
                .Build();
            serviceMessageBytes = serviceMessage.AsSpan().ToArray();
            servicePts = await channelBox.IncrementPts();
            _channelMessagesRepository.PutMessage(channelId, serviceMessage,
                servicePts);
        }

        await _unitOfWork.SaveAsync();

        if (serviceMessageBytes != null)
        {
            var memberIds = await _fanout.GetOtherActiveChannelMemberIdsAsync(channelId,
                joinerUserId);
            foreach (long memberId in memberIds)
            {
                TLUpdate memberUpdate = UpdateNewChannelMessage.Builder()
                    .Message(serviceMessageBytes)
                    .Pts(servicePts)
                    .PtsCount(1)
                    .Build();
                await _updates.EnqueueUpdate(memberId, memberUpdate);
            }
        }

        var seqCtx = _updatesContextFactory.GetUpdatesContext(authKeyId, joinerUserId);
        int seq = await seqCtx.IncrementSeq();

        var resultUpdates = new Vector();
        using (TLUpdate updateChannel = UpdateChannel.Builder().ChannelId(channelId).Build())
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
        AppendUsers(joinerUserId, ref userVector, new[] { joinerUserId, inviterAdminId });
        var chatVector = new Vector();
        chatVector.AppendTLObject(updatedChannelBytes);

        return Ferrite.TL.baseLayer.Updates.Builder()
            .UpdatesProperty(resultUpdates)
            .Users(userVector)
            .Chats(chatVector)
            .Date(date)
            .Seq(seq)
            .Build();
    }

    protected sealed record InviteAdminContext(long CurrentUserId, long ChatId, bool IsChannel,
        byte[] ChatBytes, bool IsCreator);

    protected sealed record StoredImporter(long UserId, int Date, string Link,
        string? About = null, bool Requested = false);

    protected async Task<(InviteAdminContext? Context, string? Error)> PrepareInviteAdmin(
        long authKeyId, bool isChannel, long chatId)
    {
        var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
        if (auth == null)
        {
            return (null, "AUTH_KEY_INVALID");
        }
        long currentUserId = auth.Value.AsAuthInfo().UserId;
        if (chatId <= 0)
        {
            return (null, "PEER_ID_INVALID");
        }

        byte[] chatBytes;
        {
            using var chat = await _chatRepository.GetChatAsync(chatId);
            if (isChannel)
            {
                if (chat == null || chat.Value.Type != TLChat.ChatType.Channel)
                {
                    return (null, "CHANNEL_INVALID");
                }
            }
            else if (chat == null || chat.Value.Type != TLChat.ChatType.Chat ||
                     chat.Value.AsChat().Deactivated)
            {
                return (null, "CHAT_ID_INVALID");
            }
            chatBytes = chat.Value.AsSpan().ToArray();
        }

        var participant = await _chatParticipantsRepository
            .GetParticipantAsync(chatId, currentUserId);
        if (participant == null || !IsActiveParticipant(participant.Value))
        {
            participant?.Dispose();
            return (null, "USER_NOT_PARTICIPANT");
        }

        int role = participant.Value.AsChatParticipantInfo().Role;
        bool isCreator = role == (int)ChatParticipantRole.Creator;
        bool allowed = isChannel
            ? ChatRights.HasAdminRight(participant.Value, ChatAdminRightRequirement.InviteUsers)
            : role is (int)ChatParticipantRole.Creator or (int)ChatParticipantRole.Admin;
        participant.Value.Dispose();
        if (!allowed)
        {
            return (null, "CHAT_ADMIN_REQUIRED");
        }

        return (new InviteAdminContext(currentUserId, chatId, isChannel, chatBytes,
            isCreator), null);
    }

    protected bool ImporterMatchesQuery(long userId, string query)
    {
        using var user = _userRepository.GetUser(userId);
        if (user == null)
        {
            return false;
        }
        var info = user.Value.AsUser();
        string firstName = info.FirstName.Length > 0
            ? Encoding.UTF8.GetString(info.FirstName)
            : "";
        return firstName.StartsWith(query, StringComparison.OrdinalIgnoreCase);
    }

    protected static Ferrite.TL.baseLayer.TLExportedChatInvite ErrorExportedInvite(string message) =>
        (Ferrite.TL.baseLayer.TLExportedChatInvite)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));

    protected static Ferrite.TL.baseLayer.messages.TLExportedChatInvites ErrorExportedInvites(
        string message) =>
        (Ferrite.TL.baseLayer.messages.TLExportedChatInvites)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));

    protected static Ferrite.TL.baseLayer.messages.TLExportedChatInvite ErrorMessagesExportedInvite(
        string message) =>
        (Ferrite.TL.baseLayer.messages.TLExportedChatInvite)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));

    protected static TLChatInvite ErrorChatInvite(string message) =>
        (TLChatInvite)RpcErrorGenerator.GenerateError(400, Encoding.UTF8.GetBytes(message));

    protected static Ferrite.TL.baseLayer.messages.TLChatInviteImporters ErrorChatInviteImporters(
        string message) =>
        (Ferrite.TL.baseLayer.messages.TLChatInviteImporters)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));

    protected static Ferrite.TL.baseLayer.messages.TLChatAdminsWithInvites ErrorChatAdminsWithInvites(
        string message) =>
        (Ferrite.TL.baseLayer.messages.TLChatAdminsWithInvites)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));

    protected void MarkStoredChatDeactivated(byte[] chatBytes)
    {
        using var storedChat = new TLChat(chatBytes, 0, chatBytes.Length);
        var chat = storedChat.AsChat();
        using TLChat updatedChat = chat.Clone()
            .Deactivated(true)
            .Version(chat.Version + 1)
            .Build();
        _chatRepository.PutChat(updatedChat);
    }

    protected static int ReadChatVersion(byte[] chatBytes)
    {
        using var storedChat = new TLChat(chatBytes, 0, chatBytes.Length);
        return storedChat.AsChat().Version;
    }

    protected static byte[] BuildChatParticipantsUpdateBytes(long chatId,
        IReadOnlyCollection<TLChatParticipantInfo> participants, int version)
    {
        using TLChatParticipants tlParticipants =
            BuildChatParticipants(chatId, participants, version);
        using TLUpdate update = UpdateChatParticipants.Builder()
            .Participants(tlParticipants.AsSpan())
            .Build();
        return update.AsSpan().ToArray();
    }

    protected static TLInvitedUsers ErrorInvitedUsers(string message) =>
        (TLInvitedUsers)RpcErrorGenerator.GenerateError(400, Encoding.UTF8.GetBytes(message));

    protected async Task<(BasicChatMutationContext Context, string? Error)> PrepareBasicChatMutation(
        long authKeyId, long chatId, bool requireAdmin, bool requireCreator = false)
    {
        var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
        if (auth == null)
        {
            return (BasicChatMutationContext.Empty, "AUTH_KEY_INVALID");
        }

        long currentUserId = auth.Value.AsAuthInfo().UserId;
        using var storedChat = await _chatRepository.GetChatAsync(chatId);
        if (storedChat == null || storedChat.Value.Type != TLChat.ChatType.Chat ||
            storedChat.Value.AsChat().Deactivated)
        {
            return (BasicChatMutationContext.Empty, "CHAT_ID_INVALID");
        }

        var participant = await _chatParticipantsRepository
            .GetParticipantAsync(chatId, currentUserId);
        if (participant == null || !IsActiveParticipant(participant.Value))
        {
            participant?.Dispose();
            return (BasicChatMutationContext.Empty, "USER_NOT_PARTICIPANT");
        }

        int role = participant.Value.AsChatParticipantInfo().Role;
        participant.Value.Dispose();
        if (requireCreator && role != (int)ChatParticipantRole.Creator)
        {
            return (BasicChatMutationContext.Empty, "CHAT_ADMIN_REQUIRED");
        }
        if (requireAdmin &&
            role != (int)ChatParticipantRole.Creator &&
            role != (int)ChatParticipantRole.Admin)
        {
            return (BasicChatMutationContext.Empty, "CHAT_ADMIN_REQUIRED");
        }

        var allParticipants = await _chatParticipantsRepository.GetParticipantsAsync(chatId);
        var activeParticipants = new List<TLChatParticipantInfo>();
        foreach (var participantInfo in allParticipants)
        {
            if (IsActiveParticipant(participantInfo))
            {
                activeParticipants.Add(participantInfo);
            }
            else
            {
                participantInfo.Dispose();
            }
        }

        return (new BasicChatMutationContext(currentUserId,
            storedChat.Value.AsSpan().ToArray(), activeParticipants), null);
    }

    protected async Task PutChatAbout(long chatId, byte[] about)
    {
        using var storedFullInfo = await _chatRepository.GetFullInfoAsync(chatId);
        if (storedFullInfo != null)
        {
            var fullInfo = storedFullInfo.Value.AsChatFullInfo();
            using TLChatFullInfo updated = fullInfo.Clone()
                .About(about)
                .Build();
            _chatRepository.PutFullInfo(updated);
            return;
        }

        using TLChatFullInfo created = ChatFullInfo.Builder()
            .ChatId(chatId)
            .About(about)
            .Build();
        _chatRepository.PutFullInfo(created);
    }

    protected async Task<TLUpdates> EmitBasicGroupServiceUpdates(long authKeyId, long actorUserId,
        long chatId, IReadOnlyCollection<TLChatParticipantInfo> activeParticipants,
        byte[] actionBytes, byte[] chatBytes, byte[]? sharedUpdateBytes = null)
    {
        var participantIds = new List<long>(activeParticipants.Count);
        foreach (var participantInfo in activeParticipants)
        {
            participantIds.Add(participantInfo.AsChatParticipantInfo().UserId);
        }

        int date = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
        var resultUpdateBytes = new List<byte[]>();
        var liveUpdates = new List<(long ParticipantId, byte[] UpdateBytes)>();
        foreach (long participantId in participantIds)
        {
            StoredMessageWrite write = await _messages.PutBasicGroupServiceMessageAsync(
                participantId, authKeyId, chatId, actorUserId, actionBytes, date);

            using TLUpdate updateNewMessage = UpdateNewMessage.Builder()
                .Message(write.Bytes)
                .Pts(write.Pts)
                .PtsCount(1)
                .Build();
            byte[] updateBytes = updateNewMessage.AsSpan().ToArray();
            if (participantId == actorUserId)
            {
                resultUpdateBytes.Add(updateBytes);
            }

            liveUpdates.Add((participantId, updateBytes));
        }

        await _unitOfWork.SaveAsync();
        return await _fanout.CompleteBasicGroupServiceResultAsync(actorUserId, participantIds,
            liveUpdates, resultUpdateBytes, chatBytes, sharedUpdateBytes, date);
    }

    protected static TLUpdates ErrorUpdates(string message) =>
        (TLUpdates)RpcErrorGenerator.GenerateError(400, Encoding.UTF8.GetBytes(message));

    protected static TLAffectedHistory ErrorAffectedHistory(string message) =>
        (TLAffectedHistory)RpcErrorGenerator.GenerateError(400, Encoding.UTF8.GetBytes(message));

    protected static TLBool ErrorBool(string message) =>
        (TLBool)RpcErrorGenerator.GenerateError(400, Encoding.UTF8.GetBytes(message));

    protected static void DisposeParticipants(IEnumerable<TLChatParticipantInfo> participants)
    {
        foreach (var participant in participants)
        {
            participant.Dispose();
        }
    }

    protected static List<long> ResolveInputUserIds(Vector users, long selfUserId)
    {
        var result = new List<long>(users.Count);
        for (int i = 0; i < users.Count; i++)
        {
            InputUserView user = users.ReadTLObject();
            long? userId = ResolveInputUserId(user, selfUserId);
            if (userId is > 0)
            {
                result.Add(userId.Value);
            }
        }

        return result;
    }

    protected static long? ResolveInputUserId(InputUserView user, long selfUserId)
    {
        if (user.Is(out InputUser inputUser))
        {
            return inputUser.UserId;
        }

        if (user.Is(out InputUserFromMessage fromMessage))
        {
            return fromMessage.UserId;
        }

        if (user.Is(out InputUserSelf _))
        {
            return selfUserId;
        }

        return null;
    }

    protected static bool IsActiveParticipant(TLChatParticipantInfo participant)
    {
        int role = participant.AsChatParticipantInfo().Role;
        return role != (int)ChatParticipantRole.Banned &&
               role != (int)ChatParticipantRole.Left;
    }

    protected static bool IsBasicChatAdmin(
        IReadOnlyCollection<TLChatParticipantInfo> participantInfos, long userId)
    {
        foreach (var participantInfo in participantInfos)
        {
            var info = participantInfo.AsChatParticipantInfo();
            if (info.UserId == userId)
            {
                return info.Role is (int)ChatParticipantRole.Creator
                    or (int)ChatParticipantRole.Admin;
            }
        }

        return false;
    }

    protected static TLChatParticipants BuildChatParticipants(long chatId,
        IReadOnlyCollection<TLChatParticipantInfo> participantInfos, int version)
    {
        var participantVector = new Vector();
        foreach (var participantInfo in participantInfos)
        {
            var info = participantInfo.AsChatParticipantInfo();
            using TLChatParticipant participant = info.Role switch
            {
                (int)ChatParticipantRole.Creator => ChatParticipantCreator.Builder()
                    .UserId(info.UserId)
                    .Build(),
                (int)ChatParticipantRole.Admin => ChatParticipantAdmin.Builder()
                    .UserId(info.UserId)
                    .InviterId(info.InviterId)
                    .Date(info.Date)
                    .Build(),
                _ => ChatParticipant.Builder()
                    .UserId(info.UserId)
                    .InviterId(info.InviterId)
                    .Date(info.Date)
                    .Build()
            };
            participantVector.AppendTLObject(participant.AsSpan());
        }

        return ChatParticipants.Builder()
            .ChatId(chatId)
            .Participants(participantVector)
            .Version(version)
            .Build();
    }

    protected void AppendUsers(long viewerUserId, ref Vector userVector, IEnumerable<long> userIds)
    {
        var seen = new HashSet<long>();
        foreach (long userId in userIds)
        {
            AppendUser(viewerUserId, ref userVector, seen, userId);
        }
    }

    protected bool AllUsersExist(IEnumerable<long> userIds)
    {
        foreach (long userId in userIds)
        {
            using var user = _userRepository.GetUser(userId);
            if (user == null)
            {
                return false;
            }
        }

        return true;
    }

    protected async Task<TLUpdates> SendBasicGroupMessage(long authKeyId, TLBytes q,
        long userId, long chatId, Func<Task>? afterCommit = null)
    {
        byte[] requestBytes = q.AsSpan().ToArray();
        PreparedMessageTarget target = await MessageSendTargetResolver.PrepareAsync(_chatRepository, _chatParticipantsRepository, _forumTopicsRepository, _messagingSettingsRepository, userId, TLPeer.PeerType.PeerChat, chatId, requestBytes,
            new[] { ChatBannedAction.SendMessages });
        if (target.Error != null)
        {
            return ErrorUpdates(target.Error);
        }

        ShortSentBatch sent = await _send.SendBasicGroupMessageAsync(authKeyId,
            userId, chatId, target.RelatedUserIds, requestBytes,
            chatBytes: target.ChatBytes);
        if (afterCommit != null)
        {
            await afterCommit();
        }

        return UpdateShortSentMessage.Builder()
            .OutProperty(true)
            .Id(sent.Id)
            .Pts(sent.Pts)
            .PtsCount(1)
            .Date(sent.Date)
            .Build();
    }

    protected async Task<TLUpdates> SendChannelMessage(long authKeyId, TLBytes q,
        long userId, long channelId, Func<Task>? afterCommit = null)
    {
        byte[] requestBytes = q.AsSpan().ToArray();
        PreparedMessageTarget target = await MessageSendTargetResolver.PrepareAsync(_chatRepository, _chatParticipantsRepository, _forumTopicsRepository, _messagingSettingsRepository, userId, TLPeer.PeerType.PeerChannel, channelId,
            requestBytes, new[] { ChatBannedAction.SendMessages });
        if (target.Error != null)
        {
            return ErrorUpdates(target.Error);
        }

        ChannelSentBatch sent = await _send.SendChannelMessageAsync(userId, channelId,
            target.Sender, target.Broadcast, target.ForumTopicId, target.ForumTopic,
            requestBytes, target.ChatBytes!);
        if (afterCommit != null)
        {
            await afterCommit();
        }
        return await _fanout.BuildChannelSentResultAsync(authKeyId, sent);
    }

    protected async Task<TLUpdates> UpdatePinnedChannelMessage(long authKeyId, long userId,
        long channelId, int messageId, bool pin)
    {
        var (channelBytes, _, participantBytes, contextError) =
            await GetChannelInteractionContext(userId, channelId);
        if (contextError != null)
        {
            return ErrorUpdates(contextError);
        }
        if (!ChatRights.HasAdminRight(participantBytes, ChatAdminRightRequirement.PinMessages))
        {
            return ErrorUpdates("CHAT_ADMIN_REQUIRED");
        }

        var saved = await _channelMessagesRepository
            .GetMessageAsync(channelId, messageId);
        if (saved == null)
        {
            return ErrorUpdates("MESSAGE_ID_INVALID");
        }

        using (var savedMessage = saved.Value)
        {
            var savedBody = savedMessage.AsSavedMessage();
            var original = savedBody.Get_OriginalMessage();
            if (original.Type != TLMessage.MessageType.Message)
            {
                return ErrorUpdates("MESSAGE_ID_INVALID");
            }

            using TLMessage updated = original.AsMessage().Clone()
                .Pinned(pin)
                .Build();
            _channelMessagesRepository.PutMessage(channelId, updated, savedBody.Pts);
        }

        await PutChatPinnedMessageId(channelId, pin ? messageId : 0,
            pin ? null : messageId);
        await _unitOfWork.SaveAsync();

        var channelBox = new ChannelMessageBox(_counterFactory, channelId);
        int pts = await channelBox.IncrementPts();
        var memberIds = await _fanout.GetOtherActiveChannelMemberIdsAsync(channelId, userId);
        foreach (long memberId in memberIds)
        {
            TLUpdate memberUpdate = BuildPinnedChannelMessagesUpdate(channelId,
                new[] { messageId }, pin, pts, 1);
            await _updates.EnqueueUpdate(memberId, memberUpdate);
        }

        var seqCtx = _updatesContextFactory.GetUpdatesContext(authKeyId, userId);
        int seq = await seqCtx.IncrementSeq();
        _log.Debug($"📌 UpdatePinnedMessage user:{userId} channel:{channelId} " +
                   $"id:{messageId} pinned:{pin} pts:{pts} members:{memberIds.Count}");

        var updatesVector = new Vector();
        using (TLUpdate update = BuildPinnedChannelMessagesUpdate(channelId,
                   new[] { messageId }, pin, pts, 1))
        {
            updatesVector.AppendTLObject(update.AsSpan());
        }
        var userVector = new Vector();
        AppendUsers(userId, ref userVector, new[] { userId });
        var chatVector = new Vector();
        chatVector.AppendTLObject(channelBytes);

        return Ferrite.TL.baseLayer.Updates.Builder()
            .UpdatesProperty(updatesVector)
            .Users(userVector)
            .Chats(chatVector)
            .Date((int)DateTimeOffset.Now.ToUnixTimeSeconds())
            .Seq(seq)
            .Build();
    }

    protected async Task<TLAffectedHistory> UnpinAllChannelMessages(long userId, long channelId)
    {
        var (_, _, participantBytes, contextError) =
            await GetChannelInteractionContext(userId, channelId);
        if (contextError != null)
        {
            return ErrorAffectedHistory(contextError);
        }
        if (!ChatRights.HasAdminRight(participantBytes, ChatAdminRightRequirement.PinMessages))
        {
            return ErrorAffectedHistory("CHAT_ADMIN_REQUIRED");
        }

        var saved = await _channelMessagesRepository.GetMessagesAsync(channelId);
        var unpinnedIds = new List<int>();
        foreach (var s in saved)
        {
            using var savedMessage = s;
            var savedBody = savedMessage.AsSavedMessage();
            var original = savedBody.Get_OriginalMessage();
            if (original.Type != TLMessage.MessageType.Message)
            {
                continue;
            }

            var message = original.AsMessage();
            if (!message.Pinned)
            {
                continue;
            }

            using TLMessage updated = message.Clone()
                .Pinned(false)
                .Build();
            _channelMessagesRepository.PutMessage(channelId, updated, savedBody.Pts);
            unpinnedIds.Add(message.Id);
        }

        var channelBox = new ChannelMessageBox(_counterFactory, channelId);
        if (unpinnedIds.Count == 0)
        {
            await _unitOfWork.SaveAsync();
            int currentPts = await channelBox.Pts();
            return AffectedHistory.Builder()
                .Pts(currentPts)
                .PtsCount(0)
                .Offset(0)
                .Build();
        }

        await PutChatPinnedMessageId(channelId, 0);
        await _unitOfWork.SaveAsync();

        int pts = await channelBox.IncrementPts(unpinnedIds.Count);
        var memberIds = await _fanout.GetOtherActiveChannelMemberIdsAsync(channelId, userId);
        foreach (long memberId in memberIds)
        {
            TLUpdate memberUpdate = BuildPinnedChannelMessagesUpdate(channelId,
                unpinnedIds, pinned: false, pts, unpinnedIds.Count);
            await _updates.EnqueueUpdate(memberId, memberUpdate);
        }

        _log.Debug($"📌 UnpinAllMessages user:{userId} channel:{channelId} " +
                   $"count:{unpinnedIds.Count} pts:{pts} members:{memberIds.Count}");
        return AffectedHistory.Builder()
            .Pts(pts)
            .PtsCount(unpinnedIds.Count)
            .Offset(0)
            .Build();
    }

    protected async Task<(byte[] ChannelBytes, bool Broadcast, byte[] ParticipantBytes, string? Error)>
        GetChannelInteractionContext(long userId, long channelId)
    {
        using var channel = await _chatRepository.GetChatAsync(channelId);
        if (channel == null || channel.Value.Type != TLChat.ChatType.Channel)
        {
            return (Array.Empty<byte>(), false, Array.Empty<byte>(), "CHANNEL_INVALID");
        }

        var participant = await _chatParticipantsRepository
            .GetParticipantAsync(channelId, userId);
        if (participant == null || !IsActiveParticipant(participant.Value))
        {
            participant?.Dispose();
            return (Array.Empty<byte>(), false, Array.Empty<byte>(), "CHANNEL_PRIVATE");
        }

        byte[] participantBytes = participant.Value.AsSpan().ToArray();
        participant.Value.Dispose();
        return (channel.Value.AsSpan().ToArray(), channel.Value.AsChannel().Broadcast,
            participantBytes, null);
    }

    protected static TLUpdate BuildPinnedChannelMessagesUpdate(long channelId,
        IReadOnlyList<int> messageIds, bool pinned, int pts, int ptsCount)
    {
        var ids = new VectorOfInt();
        foreach (int id in messageIds)
        {
            ids.Append(id);
        }

        return UpdatePinnedChannelMessages.Builder()
            .Pinned(pinned)
            .ChannelId(channelId)
            .Messages(ids)
            .Pts(pts)
            .PtsCount(ptsCount)
            .Build();
    }

    protected async Task<(int Pts, int Count)> DeleteConversation(long ownerId,
        TLPeer.PeerType peerType, long peerId, int maxId,
        int? minDate, int? maxDate, IUpdatesContext ownerCtx)
    {
        var deletedIds = await _messages.DeleteConversationAsync(ownerId, peerType,
            peerId, maxId, minDate, maxDate);
        await _unitOfWork.SaveAsync();

        int pts = await _fanout.AdvanceAndEnqueueDeleteMessagesAsync(ownerId,
            deletedIds, ownerCtx);
        return (pts, deletedIds.Count);
    }

    protected async Task<string?> ValidateCommonBoxPeer(long userId,
        TLPeer.PeerType peerType, long peerId, bool requireChatAdmin)
    {
        if (peerId <= 0)
        {
            return "PEER_ID_INVALID";
        }

        if (peerType == TLPeer.PeerType.PeerUser)
        {
            using var user = _userRepository.GetUser(peerId);
            return user == null ? "PEER_ID_INVALID" : null;
        }

        if (peerType == TLPeer.PeerType.PeerChat)
        {
            using var chat = await _chatRepository.GetChatAsync(peerId);
            if (chat == null || chat.Value.Type != TLChat.ChatType.Chat ||
                chat.Value.AsChat().Deactivated)
            {
                return "CHAT_ID_INVALID";
            }

            var participant = await _chatParticipantsRepository
                .GetParticipantAsync(peerId, userId);
            if (participant == null || !IsActiveParticipant(participant.Value))
            {
                participant?.Dispose();
                return "USER_NOT_PARTICIPANT";
            }

            int role = participant.Value.AsChatParticipantInfo().Role;
            participant.Value.Dispose();
            if (requireChatAdmin &&
                role != (int)ChatParticipantRole.Creator &&
                role != (int)ChatParticipantRole.Admin)
            {
                return "CHAT_ADMIN_REQUIRED";
            }

            return null;
        }

        return "PEER_ID_INVALID";
    }

    protected async Task PutChatPinnedMessageId(long chatId, int pinnedMsgId,
        int? clearOnlyIfMessageId = null)
    {
        using var storedFullInfo = await _chatRepository.GetFullInfoAsync(chatId);
        if (storedFullInfo == null)
        {
            if (pinnedMsgId <= 0)
            {
                return;
            }

            using TLChatFullInfo created = ChatFullInfo.Builder()
                .ChatId(chatId)
                .About(ReadOnlySpan<byte>.Empty)
                .PinnedMsgId(pinnedMsgId)
                .Build();
            _chatRepository.PutFullInfo(created);
            return;
        }

        var fullInfo = storedFullInfo.Value.AsChatFullInfo();
        int nextPinnedMsgId = pinnedMsgId;
        if (pinnedMsgId <= 0 && clearOnlyIfMessageId.HasValue &&
            fullInfo.PinnedMsgId != clearOnlyIfMessageId.Value)
        {
            nextPinnedMsgId = fullInfo.PinnedMsgId;
        }

        using TLChatFullInfo updated = BuildChatFullInfoWithPinned(fullInfo,
            nextPinnedMsgId);
        _chatRepository.PutFullInfo(updated);
    }

    protected static TLChatFullInfo BuildChatFullInfoWithPinned(ChatFullInfo fullInfo,
        int pinnedMsgId)
    {
        var builder = ChatFullInfo.Builder()
            .ChatId(fullInfo.ChatId)
            .About(fullInfo.About);
        if (pinnedMsgId > 0)
        {
            builder = builder.PinnedMsgId(pinnedMsgId);
        }
        if (fullInfo.Flags[1])
        {
            builder = builder.DefaultBannedRights(fullInfo.DefaultBannedRights);
        }
        if (fullInfo.Flags[2])
        {
            builder = builder.AvailableReactions(fullInfo.AvailableReactions);
        }
        if (fullInfo.Flags[3])
        {
            builder = builder.ReactionsLimit(fullInfo.ReactionsLimit);
        }
        if (fullInfo.ForumTabs)
        {
            builder = builder.ForumTabs(true);
        }
        if (fullInfo.Flags[5])
        {
            builder = builder
                .MigratedFromChatId(fullInfo.MigratedFromChatId)
                .MigratedFromMaxId(fullInfo.MigratedFromMaxId);
        }

        return builder.Build();
    }

    protected void AppendUser(long viewerUserId, ref Vector userVector, HashSet<long> seen, long userId)
    {
        if (!seen.Add(userId))
        {
            return;
        }
        using var user = _userRepository.GetUser(userId);
        if (user != null)
        {
            using var withStatus = _fanout.WithStatus(viewerUserId, user.Value);
            userVector.AppendTLObject(withStatus.AsSpan());
        }
    }

    protected async Task<List<byte[]>> GetChatBytes(IEnumerable<long> chatIds)
    {
        var chatBytes = new List<byte[]>();
        foreach (long chatId in chatIds)
        {
            using var chat = await _chatRepository.GetChatAsync(chatId);
            if (chat != null)
            {
                chatBytes.Add(chat.Value.AsSpan().ToArray());
            }
        }

        return chatBytes;
    }

    protected async Task<List<byte[]>> GetChatBytesForViewer(long viewerUserId,
        IEnumerable<long> chatIds)
    {
        var chatBytes = new List<byte[]>();
        foreach (long chatId in chatIds)
        {
            byte[]? rowBytes;
            bool isChannel;
            {
                using var chat = await _chatRepository.GetChatAsync(chatId);
                if (chat == null)
                {
                    continue;
                }
                rowBytes = chat.Value.AsSpan().ToArray();
                isChannel = chat.Value.Type == TLChat.ChatType.Channel;
            }
            if (isChannel)
            {
                rowBytes = await ChannelRows.ForViewerAsync(
                    _chatParticipantsRepository, viewerUserId, chatId, rowBytes);
            }
            chatBytes.Add(rowBytes);
        }

        return chatBytes;
    }

    protected static void AddMessageRelatedPeers(TLMessage message, HashSet<long> userIds,
        HashSet<long> chatIds)
    {
        if (!MessageStore.TryReadStoredMessageInfo(message, out var info))
        {
            return;
        }

        if (info.PeerType == TLPeer.PeerType.PeerUser)
        {
            userIds.Add(info.PeerId);
        }
        else if (info.PeerType is TLPeer.PeerType.PeerChat or TLPeer.PeerType.PeerChannel)
        {
            chatIds.Add(info.PeerId);
        }

        if (message.Type == TLMessage.MessageType.Message)
        {
            var regular = message.AsMessage();
            if (regular.Flags[8] &&
                TryReadPeer(regular.Get_FromIdView(), out var regularFromPeer))
            {
                if (regularFromPeer.Type == TLPeer.PeerType.PeerUser)
                {
                    userIds.Add(regularFromPeer.Id);
                }
                else
                {
                    chatIds.Add(regularFromPeer.Id);
                }
            }
            return;
        }

        if (message.Type != TLMessage.MessageType.MessageService)
        {
            return;
        }

        var service = message.AsMessageService();
        if (service.Flags[8] && TryReadPeer(service.Get_FromIdView(), out var fromPeer))
        {
            if (fromPeer.Type == TLPeer.PeerType.PeerUser)
            {
                userIds.Add(fromPeer.Id);
            }
            else
            {
                chatIds.Add(fromPeer.Id);
            }
        }
        MessageActionView action = service.Get_ActionView();
        foreach (long actionUserId in ReadActionUserIds(action))
        {
            userIds.Add(actionUserId);
        }
        foreach (long actionChatId in ReadActionChatIds(action))
        {
            chatIds.Add(actionChatId);
        }
    }

    protected static List<long> ReadActionUserIds(MessageActionView action)
    {
        var userIds = new List<long>();
        if (action.Is(out MessageActionChatCreate create))
        {
            var users = create.Users;
            for (int i = 0; i < users.Count; i++)
            {
                userIds.Add(users[i]);
            }
        }
        else if (action.Is(out MessageActionChatAddUser addUser))
        {
            var users = addUser.Users;
            for (int i = 0; i < users.Count; i++)
            {
                userIds.Add(users[i]);
            }
        }
        else if (action.Is(out MessageActionChatDeleteUser deleteUser))
        {
            userIds.Add(deleteUser.UserId);
        }
        else if (action.Is(out MessageActionChatJoinedByLink joinedByLink))
        {
            userIds.Add(joinedByLink.InviterId);
        }

        return userIds;
    }

    protected static List<long> ReadActionChatIds(MessageActionView action)
    {
        var chatIds = new List<long>();
        if (action.Is(out MessageActionChatMigrateTo migrateTo))
        {
            chatIds.Add(migrateTo.ChannelId);
        }
        else if (action.Is(out MessageActionChannelMigrateFrom migrateFrom))
        {
            chatIds.Add(migrateFrom.ChatId);
        }

        return chatIds;
    }

    protected static bool TryReadPeer(PeerView peer, out (TLPeer.PeerType Type, long Id) value)
    {
        if (peer.Is(out PeerUser user))
        {
            value = (TLPeer.PeerType.PeerUser, user.UserId);
            return true;
        }
        if (peer.Is(out PeerChat chat))
        {
            value = (TLPeer.PeerType.PeerChat, chat.ChatId);
            return true;
        }
        if (peer.Is(out PeerChannel channel))
        {
            value = (TLPeer.PeerType.PeerChannel, channel.ChannelId);
            return true;
        }

        value = default;
        return false;
    }

    protected sealed record BasicChatMutationContext(
        long CurrentUserId,
        byte[] ChatBytes,
        List<TLChatParticipantInfo> ActiveParticipants)
    {
        public static readonly BasicChatMutationContext Empty = new(0, Array.Empty<byte>(), new());
    }

    protected static long ResolvePeerUserId(InputPeerView peer, long selfUserId)
    {
        if (peer.Is(out InputPeerSelf _)) return selfUserId;
        if (peer.Is(out InputPeerUser user)) return user.UserId;
        throw new NotImplementedException();
    }

    protected static long? ResolveOptionalPeerUserId(InputPeerView peer, long selfUserId)
    {
        if (peer.Is(out InputPeerEmpty _)) return null;
        if (peer.Is(out InputPeerSelf _)) return selfUserId;
        if (peer.Is(out InputPeerUser user)) return user.UserId;
        throw new NotImplementedException();
    }

    protected static long GetPeerId(TLPeer p) => p.Type switch
    {
        TLPeer.PeerType.PeerChat => p.AsPeerChat().ChatId,
        TLPeer.PeerType.PeerUser => p.AsPeerUser().UserId,
        TLPeer.PeerType.PeerChannel => p.AsPeerChannel().ChannelId,
        _ => 0
    };
}
