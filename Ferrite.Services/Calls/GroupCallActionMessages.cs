// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;

namespace Ferrite.Services.Calls;

public sealed class GroupCallActionMessages
{
    private readonly IChannelMessagesRepository _channelMessagesRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICounterFactory _counterFactory;
    private readonly IUpdatesContextFactory _updatesContextFactory;
    private readonly MessageStore _messages;
    private readonly UpdateFanout _fanout;

    public GroupCallActionMessages(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, ICounterFactory counterFactory,
        IUpdatesContextFactory updatesContextFactory, MessageStore messages,
        UpdateFanout fanout)
    {
        _channelMessagesRepository = channelMessagesRepository;

        _unitOfWork = unitOfWork;
        _counterFactory = counterFactory;
        _updatesContextFactory = updatesContextFactory;
        _messages = messages;
        _fanout = fanout;
    }

    public static TLMessageAction BuildCallAction(TLInputGroupCall call,
        int? duration = null)
    {
        var builder = MessageActionGroupCall.Builder().Call(call.AsSpan());
        if (duration is { } seconds)
        {
            builder = builder.Duration(seconds);
        }

        return builder.Build();
    }

    public static TLMessageAction BuildScheduledAction(TLInputGroupCall call,
        int scheduleDate) => MessageActionGroupCallScheduled.Builder()
        .Call(call.AsSpan())
        .ScheduleDate(scheduleDate)
        .Build();

    public static TLMessageAction BuildInviteAction(TLInputGroupCall call,
        IReadOnlyCollection<long> invitedUserIds)
    {
        var users = new VectorOfLong();
        foreach (long userId in invitedUserIds)
        {
            users.Append(userId);
        }

        return MessageActionInviteToGroupCall.Builder()
            .Call(call.AsSpan())
            .Users(users)
            .Build();
    }

    public Task<TLUpdates> EmitAsync(long authKeyId, long actorUserId,
        GroupCallPeerKind kind, long peerId, byte[] chatBytes, byte[] actionBytes,
        IReadOnlyList<byte[]>? leadingCallerUpdates = null,
        IReadOnlyCollection<long>? relatedUserIds = null,
        bool peerStateChanged = true) =>
        kind == GroupCallPeerKind.BasicGroup
            ? EmitBasicGroupActionAsync(authKeyId, actorUserId, peerId, chatBytes,
                actionBytes, leadingCallerUpdates, relatedUserIds, peerStateChanged)
            : EmitChannelActionAsync(authKeyId, actorUserId, peerId, chatBytes,
                actionBytes, leadingCallerUpdates, relatedUserIds, peerStateChanged);

    private async Task<TLUpdates> EmitBasicGroupActionAsync(long authKeyId,
        long actorUserId, long chatId, byte[] chatBytes, byte[] actionBytes,
        IReadOnlyList<byte[]>? leadingCallerUpdates,
        IReadOnlyCollection<long>? relatedUserIds, bool peerStateChanged)
    {
        List<long> memberIds = await _fanout.GetActiveMemberIdsAsync(chatId,
            excludeUserId: null);
        int date = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
        var callerUpdateBytes = new List<byte[]>();
        if (leadingCallerUpdates != null)
        {
            callerUpdateBytes.AddRange(leadingCallerUpdates);
        }
        var liveUpdates = new List<(long MemberId, byte[] UpdateBytes)>();
        foreach (long memberId in memberIds)
        {
            StoredMessageWrite write = await _messages.PutBasicGroupServiceMessageAsync(
                memberId, authKeyId, chatId, actorUserId, actionBytes, date);
            using TLUpdate updateNewMessage = UpdateNewMessage.Builder()
                .Message(write.Bytes)
                .Pts(write.Pts)
                .PtsCount(1)
                .Build();
            byte[] updateBytes = updateNewMessage.AsSpan().ToArray();
            if (memberId == actorUserId)
            {
                callerUpdateBytes.Add(updateBytes);
            }

            liveUpdates.Add((memberId, updateBytes));
        }

        await _unitOfWork.SaveAsync();

        if (peerStateChanged)
        {
            await _fanout.PushUpdateChatAsync(chatId,
                memberIds.Where(id => id != actorUserId));
        }
        IEnumerable<long> resultUserIds = relatedUserIds == null
            ? memberIds
            : memberIds.Concat(relatedUserIds);
        return await _fanout.CompleteBasicGroupServiceResultAsync(actorUserId,
            resultUserIds.Distinct().ToArray(), liveUpdates, callerUpdateBytes,
            chatBytes, sharedUpdateBytes: null, date);
    }

    private async Task<TLUpdates> EmitChannelActionAsync(long authKeyId,
        long actorUserId, long channelId, byte[] channelBytes, byte[] actionBytes,
        IReadOnlyList<byte[]>? leadingCallerUpdates,
        IReadOnlyCollection<long>? relatedUserIds, bool peerStateChanged)
    {
        int date = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
        StoredMessageWrite write = await MessageStore.PutChannelServiceMessageAsync(_channelMessagesRepository, _counterFactory, channelId, actorUserId, actionBytes, date);

        await _unitOfWork.SaveAsync();

        await _fanout.PushChannelServiceMessageAsync(channelId, actorUserId, write.Bytes,
            write.Pts);
        if (peerStateChanged)
        {
            await _fanout.PushUpdateChannelToOtherMembersAsync(channelId, actorUserId);
        }

        int seq = await _updatesContextFactory
            .GetUpdatesContext(authKeyId, actorUserId).IncrementSeq();
        var callerUpdateBytes = new List<byte[]>(2);
        if (leadingCallerUpdates != null)
        {
            callerUpdateBytes.AddRange(leadingCallerUpdates);
        }
        using (TLUpdate updateNewChannelMessage = UpdateNewChannelMessage.Builder()
                   .Message(write.Bytes)
                   .Pts(write.Pts)
                   .PtsCount(1)
                   .Build())
        {
            callerUpdateBytes.Add(updateNewChannelMessage.AsSpan().ToArray());
        }
        if (peerStateChanged)
        {
            using TLUpdate updateChannel = UpdateChannel.Builder()
                .ChannelId(channelId)
                .Build();
            callerUpdateBytes.Add(updateChannel.AsSpan().ToArray());
        }

        IEnumerable<long> resultUserIds = relatedUserIds == null
            ? new[] { actorUserId }
            : relatedUserIds.Prepend(actorUserId);
        return _fanout.BuildUpdates(actorUserId, callerUpdateBytes, resultUserIds,
            new[] { channelBytes }, date, seq);
    }
}
