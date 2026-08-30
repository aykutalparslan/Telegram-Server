// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System;
using Ferrite.Data.Repositories;
using Ferrite.Services.SecretChats.Handlers;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.updates;
using Ferrite.Utils;
using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Handlers.UpdateMethods;

public sealed class GetDifferenceHandler : UpdatesHandlerBase
{
    private readonly IMessageRepository _messageRepository;
    private readonly IUpdatesStateRepository _updatesStateRepository;

    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatRepository _chatRepository;
    private readonly ISecretChatsRepository _secretChatsRepository;
    private readonly IUserRepository _userRepository;

    private readonly ISecretChatTransitionRepair _secretChatTransitionRepair;

    private readonly UserSerializer _userSerializer;

    private byte[] WithStatus(long viewerUserId, TLUser user)
    {
        using var hydrated = _userSerializer.WithStatus(viewerUserId, user);
        return hydrated.AsSpan().ToArray();
    }

    public GetDifferenceHandler(IMTProtoTime time, IUnitOfWork unitOfWork, IMessageRepository messageRepository, IUpdatesStateRepository updatesStateRepository, IAuthorizationRepository authorizationRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IContactsRepository contactsRepository, ISecretChatsRepository secretChatsRepository, IUserRepository userRepository, IUserStatusRepository userStatusRepository,
        IUpdatesContextFactory updatesContextFactory, ICounterFactory counterFactory,
        ILogger log, ISecretChatTransitionRepair secretChatTransitionRepair)
        : base(time, unitOfWork, chatParticipantsRepository, chatRepository, updatesContextFactory, counterFactory, log)
    {
        _userSerializer = new UserSerializer(userRepository, userStatusRepository, contactsRepository);
        _messageRepository = messageRepository;
        _updatesStateRepository = updatesStateRepository;

        _authorizationRepository = authorizationRepository;
        _chatRepository = chatRepository;
        _secretChatsRepository = secretChatsRepository;
        _userRepository = userRepository;

        _secretChatTransitionRepair = secretChatTransitionRepair;
    }

    [TLFunction(Constructors.layer58_UpdatesGetDifference)]
    public async Task<TLDifference> HandleLayer58(long authKeyId, TLBytes q)
    {
        using var current = ToCurrentDifferenceRequest(q);
        return await Handle(authKeyId, current);
    }

    private static TLBytes ToCurrentDifferenceRequest(TLBytes q)
    {
        var sent = new TL.layer58.updates.UpdatesGetDifference(q.AsSpan());
        var builder = UpdatesGetDifference.Builder()
            .Pts(sent.Pts)
            .Date(sent.Date)
            .Qts(sent.Qts);
        if (sent.Flags[0])
        {
            builder = builder.PtsTotalLimit(sent.PtsTotalLimit);
        }
        var current = builder.Build();
        return current.TLBytes!.Value;
    }

    [TLFunction(Constructors.baseLayer_UpdatesGetDifference)]
    public async Task<TLDifference> Handle(long authKeyId, TLBytes q)
    {
        var request = (UpdatesGetDifference)q;
        int requestDate = request.Date;
        int requestPts = request.Pts;
        int perSliceLimit = request.Flags[1]
            ? Math.Max(0, request.PtsLimit)
            : int.MaxValue;
        int totalLimit = request.Flags[0]
            ? Math.Max(0, request.PtsTotalLimit)
            : int.MaxValue;
        int ptsLimit = Math.Min(perSliceLimit, totalLimit);
        int requestQts = request.Qts;
        int qtsLimit = request.Flags[2]
            ? Math.Max(0, request.QtsLimit)
            : int.MaxValue;

        using TLAuthInfo? auth =
            await _authorizationRepository.GetAuthorizationAsync(authKeyId);
        if (auth is null)
        {
            return (TLDifference)RpcErrorGenerator.GenerateError(400,
                "AUTH_KEY_INVALID"u8);
        }

        long userId = auth.Value.AsAuthInfo().UserId;
        IUpdatesContext updatesCtx =
            _updatesContextFactory.GetUpdatesContext(authKeyId, userId);
        int currentDate = (int)_time.GetUnixTimeInSeconds();
        int currentPts = await CommonUpdatesState.GetCommittedPts(_updatesStateRepository, _messageRepository, updatesCtx, userId);
        bool isProbe = requestDate == int.MaxValue;
        if (!isProbe)
        {
            await _secretChatTransitionRepair.RepairAsync(authKeyId);
        }

        SecretChatQtsDifferenceResult qtsDifference = await _secretChatsRepository.ReadQtsDifferenceAsync(authKeyId, requestQts,
                currentDate, qtsLimit, updatesCtx.Qts, updatesCtx.IncrementQts);
        int acknowledgedQts = qtsDifference.State.AsSecretChatQtsState()
            .AcknowledgedQts;
        int finalQts = qtsDifference.HighWaterQts;
        var encryptedMessages = new List<(int Qts, byte[] Message)>();
        foreach (TLDto.TLSecretChatQtsEntry entry in qtsDifference.Entries)
        {
            using (entry)
            {
                TLDto.SecretChatQtsEntry row = entry.AsSecretChatQtsEntry();
                encryptedMessages.Add((row.Qts, row.EncryptedMessage.ToArray()));
            }
        }
        qtsDifference.State.Dispose();

        SecretChatControlDifferenceResult controlDifference = await _secretChatsRepository.GetControlDifferenceAsync(authKeyId, requestDate,
                currentDate, currentDate, isProbe);
        var controlUpdates = new List<(int ChatId, byte[] Update)>();
        foreach (TLDto.TLSecretChatControlUpdate control in controlDifference.Updates)
        {
            using (control)
            {
                TLDto.SecretChatControlUpdate row =
                    control.AsSecretChatControlUpdate();
                controlUpdates.Add((row.ChatId, row.Update.ToArray()));
            }
        }

        CommonMessageDifference commonDifference = await ReadCommonMessages(
            userId, requestPts, currentPts, currentDate, ptsLimit);

        List<(long ChannelId, int Pts, byte[] ChannelBytes)> channelMarkers = isProbe
            ? []
            : await GatherChannelTooLong(userId);
        List<byte[]> relatedUsers = await GatherControlUsers(userId, controlUpdates);
        (List<byte[]> messageUsers, List<byte[]> messageChats) =
            await GatherMessageRelations(userId, commonDifference.Messages);
        relatedUsers.AddRange(messageUsers);

        await _unitOfWork.SaveAsync();

        bool needsStateAdvance = finalQts > requestQts ||
                                 acknowledgedQts > requestQts ||
                                 currentPts > requestPts;
        if (commonDifference.Messages.Count == 0 &&
            encryptedMessages.Count == 0 && controlUpdates.Count == 0 &&
            channelMarkers.Count == 0 && !qtsDifference.HasMore &&
            !needsStateAdvance)
        {
            int seq = await updatesCtx.Seq();
            return DifferenceEmpty.Builder()
                .Date(currentDate)
                .Seq(seq)
                .Build();
        }

        int stateQts = qtsDifference.HasMore
            ? encryptedMessages.Count == 0
                ? acknowledgedQts
                : encryptedMessages[^1].Qts
            : finalQts;
        int statePts = commonDifference.HasMore
            ? commonDifference.Messages.Count == 0
                ? requestPts
                : commonDifference.Messages.Max(x => x.Pts)
            : currentPts;
        using TLState state = await BuildState(updatesCtx, statePts, currentDate,
            stateQts);

        var newMessages = new Vector();
        foreach (CommonMessage message in commonDifference.Messages)
        {
            newMessages.AppendTLObject(message.Bytes);
        }

        var newEncryptedMessages = new Vector();
        foreach ((_, byte[] message) in encryptedMessages)
        {
            newEncryptedMessages.AppendTLObject(message);
        }

        var otherUpdates = new Vector();
        foreach ((_, byte[] update) in controlUpdates)
        {
            otherUpdates.AppendTLObject(update);
        }
        foreach ((long channelId, int pts, _) in channelMarkers)
        {
            using var tooLong = UpdateChannelTooLong.Builder()
                .ChannelId(channelId)
                .Pts(pts)
                .Build();
            otherUpdates.AppendTLObject(tooLong.ToReadOnlySpan());
        }

        var chats = new Vector();
        foreach (byte[] messageChat in messageChats)
        {
            chats.AppendTLObject(messageChat);
        }
        foreach ((_, _, byte[] channelBytes) in channelMarkers)
        {
            chats.AppendTLObject(channelBytes);
        }

        var users = new Vector();
        foreach (byte[] user in relatedUsers)
        {
            users.AppendTLObject(user);
        }

        bool hasMore = commonDifference.HasMore || qtsDifference.HasMore;
        _log.Debug($"/// GetDifference user:{userId} messages:{commonDifference.Messages.Count} " +
                   $"encrypted:{encryptedMessages.Count} " +
                   $"controls:{controlUpdates.Count} channelTooLong:{channelMarkers.Count} " +
                   $"slice:{hasMore} pts:{statePts} qts:{stateQts} ///");
        if (hasMore)
        {
            return DifferenceSlice.Builder()
                .NewMessages(newMessages)
                .NewEncryptedMessages(newEncryptedMessages)
                .OtherUpdates(otherUpdates)
                .Chats(chats)
                .Users(users)
                .IntermediateState(state.AsSpan())
                .Build();
        }

        return Difference.Builder()
            .NewMessages(newMessages)
            .NewEncryptedMessages(newEncryptedMessages)
            .OtherUpdates(otherUpdates)
            .Chats(chats)
            .Users(users)
            .State(state.AsSpan())
            .Build();
    }

    private async Task<CommonMessageDifference> ReadCommonMessages(long userId,
        int requestPts, int currentPts, int currentDate, int limit)
    {
        if (requestPts >= currentPts || requestPts == int.MaxValue)
        {
            return new CommonMessageDifference([], false);
        }

        IReadOnlyCollection<TLSavedMessage> rows = await _messageRepository.GetMessagesAsync(userId, requestPts + 1, currentPts,
                DateTimeOffset.FromUnixTimeSeconds(currentDate));
        var messages = new List<CommonMessage>(rows.Count);
        foreach (TLSavedMessage row in rows)
        {
            using (row)
            {
                SavedMessage saved = row.AsSavedMessage();
                using TLMessage message = saved.Get_OriginalMessage();
                messages.Add(new CommonMessage(saved.Pts,
                    MessageIds.GetId(message), message.AsSpan().ToArray()));
            }
        }

        messages.Sort(static (left, right) => left.MessageId != right.MessageId
            ? left.MessageId.CompareTo(right.MessageId)
            : left.Pts.CompareTo(right.Pts));
        bool hasMore = messages.Count > limit;
        if (hasMore)
        {
            messages = messages.Take(limit).ToList();
        }
        return new CommonMessageDifference(messages, hasMore);
    }

    private async Task<(List<byte[]> Users, List<byte[]> Chats)>
        GatherMessageRelations(long viewerUserId, IReadOnlyList<CommonMessage> messages)
    {
        var userIds = new HashSet<long>();
        var chatIds = new HashSet<long>();
        foreach (CommonMessage commonMessage in messages)
        {
            using TLMessage message = new(commonMessage.Bytes, 0,
                commonMessage.Bytes.Length);
            (List<long> messageUserIds, List<long> messageChatIds) =
                UpdatesService.ReadMessageRelationIds(message);
            userIds.UnionWith(messageUserIds.Where(id => id > 0));
            chatIds.UnionWith(messageChatIds.Where(id => id > 0));
        }

        var users = new List<byte[]>(userIds.Count);
        foreach (long relatedUserId in userIds)
        {
            using TLUser? user = _userRepository.GetUser(relatedUserId);
            if (user is not null)
            {
                users.Add(WithStatus(viewerUserId, user.Value));
            }
        }

        var chats = new List<byte[]>(chatIds.Count);
        foreach (long chatId in chatIds)
        {
            using TLChat? chat = await _chatRepository.GetChatAsync(chatId);
            if (chat is not null)
            {
                chats.Add(chat.Value.AsSpan().ToArray());
            }
        }
        return (users, chats);
    }

    private static async Task<TLState> BuildState(IUpdatesContext updatesCtx,
        int pts, int date, int qts)
    {
        int seq = await updatesCtx.Seq();
        int unreadCount = await updatesCtx.UnreadMessages();
        return State.Builder()
            .Pts(pts)
            .Qts(qts)
            .Date(date)
            .Seq(seq)
            .UnreadCount(unreadCount)
            .Build();
    }

    private async Task<List<byte[]>> GatherControlUsers(long viewerUserId,
        IReadOnlyList<(int ChatId, byte[] Update)> controls)
    {
        var userIds = new HashSet<long>();
        foreach (int chatId in controls.Select(x => x.ChatId).Distinct())
        {
            using TLDto.TLSecretChatState? chat =
                await _secretChatsRepository.GetChatAsync(chatId);
            if (chat is null)
            {
                continue;
            }
            TLDto.SecretChatState row = chat.Value.AsSecretChatState();
            userIds.Add(row.InitiatorUserId);
            userIds.Add(row.RecipientUserId);
        }

        var users = new List<byte[]>();
        foreach (long userId in userIds)
        {
            using TLUser? user = _userRepository.GetUser(userId);
            if (user is not null)
            {
                users.Add(WithStatus(viewerUserId, user.Value));
            }
        }
        return users;
    }

    private readonly record struct CommonMessage(int Pts, int MessageId,
        byte[] Bytes);

    private readonly record struct CommonMessageDifference(
        List<CommonMessage> Messages, bool HasMore);
}
