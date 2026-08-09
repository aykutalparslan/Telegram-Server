// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.MessageMethods;

/// <summary>
/// Sets or clears a conversation's auto-delete timer. The period is stored on the
/// shared conversation row, announced with a `messageActionSetMessagesTTL` service
/// message, and reported to every side with `updatePeerHistoryTTL`. Only messages
/// created after the change inherit it: nothing rewrites history, because a client
/// already holds the rows it was given without a `ttl_period`.
/// </summary>
public sealed class SetHistoryTTLHandler : MessagesHandlerBase
{
    private readonly IChannelMessagesRepository _channelMessagesRepository;

    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IUserRepository _userRepository;

    private readonly ChatSettingsStore _settings;
    private readonly TimeProvider _timeProvider;

    public SetHistoryTTLHandler(IUnitOfWork unitOfWork, IForumTopicsRepository forumTopicsRepository, IMessagingSettingsRepository messagingSettingsRepository, IAuthorizationRepository authorizationRepository, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository, ISearchEngine search,
        IUpdatesService updates, IUpdatesContextFactory updatesContextFactory,
        ILogger log, IUploadService upload, IPhotoProcessingService photos,
        ICounterFactory counterFactory, IdAllocators ids,
        ChatRowStore chatRows, InviteStore invites,
        PrivacyEvaluator privacy, MessageStore messages, SendPipeline send,
        UpdateFanout fanout, DialogBuilder dialogs, ChatSettingsStore settings,
        TimeProvider timeProvider)
        : base(unitOfWork, forumTopicsRepository, messagingSettingsRepository, authorizationRepository, channelMessagesRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, search, updates, updatesContextFactory, log, upload, photos, counterFactory, ids, chatRows, invites, privacy, messages, send, fanout, dialogs)
    {
        _channelMessagesRepository = channelMessagesRepository;

        _authorizationRepository = authorizationRepository;
        _userRepository = userRepository;

        _settings = settings;
        _timeProvider = timeProvider;
    }

    [TLFunction(Constructors.baseLayer_SetHistoryTTL)]
    public async Task<TLUpdates> Handle(long authKeyId, TLBytes q)
    {
        long userId;
        using (TLAuthInfo? auth = await _authorizationRepository
                   .GetAuthorizationAsync(authKeyId))
        {
            if (auth == null || !auth.Value.AsAuthInfo().LoggedIn)
            {
                return ErrorUpdates("AUTH_KEY_INVALID");
            }
            userId = auth.Value.AsAuthInfo().UserId;
        }

        var request = (SetHistoryTTL)q;
        DialogPeerKey? destination = PeerResolver.ResolveOptionalDialogPeer(
            request.Get_PeerView(), userId);
        int period = request.Period;
        if (destination == null || destination.Value.Id <= 0)
        {
            return ErrorUpdates("PEER_ID_INVALID");
        }
        if (period < 0)
        {
            return ErrorUpdates("TTL_PERIOD_INVALID");
        }

        return destination.Value.Type switch
        {
            TLPeer.PeerType.PeerUser => await SetPrivateTtlAsync(authKeyId, userId,
                destination.Value.Id, period),
            TLPeer.PeerType.PeerChat => await SetBasicGroupTtlAsync(authKeyId,
                destination.Value.Id, period),
            TLPeer.PeerType.PeerChannel => await SetChannelTtlAsync(authKeyId, userId,
                destination.Value.Id, period),
            _ => ErrorUpdates("PEER_ID_INVALID")
        };
    }

    private async Task<TLUpdates> SetPrivateTtlAsync(long authKeyId, long userId,
        long peerUserId, int period)
    {
        // Saved Messages has no second party to keep a timer with, and pinned TDLib
        // refuses the request locally (`MessagesManager.cpp:29452-29455`).
        if (peerUserId == userId)
        {
            return ErrorUpdates("PEER_ID_INVALID");
        }
        using (TLUser? peer = _userRepository.GetUser(peerUserId))
        {
            if (peer == null || peer.Value.Type != TLUser.UserType.User)
            {
                return ErrorUpdates("PEER_ID_INVALID");
            }
        }

        ChatSettingsScope scope = ChatSettingsScope.ForPrivatePair(userId, peerUserId);
        ChatSettingsSnapshot current = await _settings.GetAsync(scope);
        _settings.Put(scope, current with { TtlPeriod = period });

        byte[] actionBytes = BuildAction(period);
        int date = checked((int)_timeProvider.GetUtcNow().ToUnixTimeSeconds());
        StoredMessageWrite callerWrite = await _messages.PutPrivateServiceMessageAsync(
            userId, authKeyId, peerUserId, userId, outgoing: true, actionBytes, date);
        StoredMessageWrite peerWrite = await _messages.PutPrivateServiceMessageAsync(
            peerUserId, null, userId, userId, outgoing: false, actionBytes, date);
        long logicalId = await _messages.CreateMessageCopyAsync(userId, callerWrite.Id);
        _messages.PutMessageCopy(logicalId, peerUserId, peerWrite.Id);
        if (!await _unitOfWork.SaveAsync())
        {
            return ErrorUpdates("INTERNAL_SERVER_ERROR");
        }

        byte[] callerPeerBytes = BuildPeerBytes(TLPeer.PeerType.PeerUser, peerUserId);
        byte[] peerSidePeerBytes = BuildPeerBytes(TLPeer.PeerType.PeerUser, userId);
        await _fanout.EnqueueNewMessageAsync(peerUserId, peerWrite.Bytes,
            peerWrite.Pts);
        await _updates.EnqueueUpdate(peerUserId,
            BuildHistoryTtlUpdate(peerSidePeerBytes, period));

        int seq = await _updatesContextFactory.GetUpdatesContext(authKeyId, userId)
            .IncrementSeq();
        var updateBytes = new List<byte[]>(2);
        using (TLUpdate updateNewMessage = UpdateNewMessage.Builder()
                   .Message(callerWrite.Bytes)
                   .Pts(callerWrite.Pts)
                   .PtsCount(1)
                   .Build())
        {
            updateBytes.Add(updateNewMessage.AsSpan().ToArray());
        }
        using (TLUpdate historyTtl = BuildHistoryTtlUpdate(callerPeerBytes, period))
        {
            updateBytes.Add(historyTtl.AsSpan().ToArray());
        }

        _log.Debug($"⌛ SetHistoryTTL user:{userId} peer:{peerUserId} period:{period}");
        return _fanout.BuildUpdates(updateBytes, new[] { userId, peerUserId },
            Array.Empty<byte[]>(), date, seq);
    }

    private async Task<TLUpdates> SetBasicGroupTtlAsync(long authKeyId, long chatId,
        int period)
    {
        var (context, error) = await PrepareBasicChatMutation(authKeyId, chatId,
            requireAdmin: true);
        if (error != null)
        {
            return ErrorUpdates(error);
        }

        try
        {
            ChatSettingsScope scope = ChatSettingsScope.ForChat(chatId);
            ChatSettingsSnapshot current = await _settings.GetAsync(scope);
            _settings.Put(scope, current with { TtlPeriod = period });

            byte[] peerBytes = BuildPeerBytes(TLPeer.PeerType.PeerChat, chatId);
            byte[] historyTtlBytes;
            using (TLUpdate historyTtl = BuildHistoryTtlUpdate(peerBytes, period))
            {
                historyTtlBytes = historyTtl.AsSpan().ToArray();
            }

            _log.Debug($"⌛ SetHistoryTTL user:{context.CurrentUserId} chat:{chatId} " +
                       $"period:{period}");
            return await EmitBasicGroupServiceUpdates(authKeyId, context.CurrentUserId,
                chatId, context.ActiveParticipants, BuildAction(period),
                context.ChatBytes, historyTtlBytes);
        }
        finally
        {
            DisposeParticipants(context.ActiveParticipants);
        }
    }

    private async Task<TLUpdates> SetChannelTtlAsync(long authKeyId, long userId,
        long channelId, int period)
    {
        var (channelBytes, _, participantBytes, error) =
            await GetChannelInteractionContext(userId, channelId);
        if (error != null)
        {
            return ErrorUpdates(error);
        }
        if (!ChatRights.HasAdminRight(participantBytes,
                ChatAdminRightRequirement.ChangeInfo))
        {
            return ErrorUpdates("CHAT_ADMIN_REQUIRED");
        }

        ChatSettingsScope scope = ChatSettingsScope.ForChannel(channelId);
        ChatSettingsSnapshot current = await _settings.GetAsync(scope);
        _settings.Put(scope, current with { TtlPeriod = period });

        int date = checked((int)_timeProvider.GetUtcNow().ToUnixTimeSeconds());
        StoredMessageWrite write = await MessageStore.PutChannelServiceMessageAsync(_channelMessagesRepository, _counterFactory, channelId, userId, BuildAction(period),
            date);
        if (!await _unitOfWork.SaveAsync())
        {
            return ErrorUpdates("INTERNAL_SERVER_ERROR");
        }

        byte[] peerBytes = BuildPeerBytes(TLPeer.PeerType.PeerChannel, channelId);
        await _fanout.PushChannelServiceMessageAsync(channelId, userId, write.Bytes,
            write.Pts);
        foreach (long memberId in await _fanout.GetOtherActiveChannelMemberIdsAsync(
                     channelId, userId))
        {
            await _updates.EnqueueUpdate(memberId,
                BuildHistoryTtlUpdate(peerBytes, period));
        }

        int seq = await _updatesContextFactory.GetUpdatesContext(authKeyId, userId)
            .IncrementSeq();
        var updateBytes = new List<byte[]>(2);
        using (TLUpdate updateNewChannelMessage = UpdateNewChannelMessage.Builder()
                   .Message(write.Bytes)
                   .Pts(write.Pts)
                   .PtsCount(1)
                   .Build())
        {
            updateBytes.Add(updateNewChannelMessage.AsSpan().ToArray());
        }
        using (TLUpdate historyTtl = BuildHistoryTtlUpdate(peerBytes, period))
        {
            updateBytes.Add(historyTtl.AsSpan().ToArray());
        }

        _log.Debug($"⌛ SetHistoryTTL user:{userId} channel:{channelId} " +
                   $"period:{period}");
        return _fanout.BuildUpdates(updateBytes, new[] { userId },
            new[] { channelBytes }, date, seq);
    }

    private static byte[] BuildPeerBytes(TLPeer.PeerType peerType, long peerId)
    {
        using TLPeer peer = PeerResolver.BuildPeer(peerType, peerId);
        return peer.AsSpan().ToArray();
    }

    // A cleared timer is reported as the flag being absent rather than as zero,
    // which is how the pinned client distinguishes "no timer" from "not told".
    private static TLUpdate BuildHistoryTtlUpdate(byte[] peerBytes, int period)
    {
        var builder = UpdatePeerHistoryTTL.Builder().Peer(peerBytes);
        if (period > 0)
        {
            builder = builder.TtlPeriod(period);
        }
        return builder.Build();
    }

    private static byte[] BuildAction(int period)
    {
        using TLMessageAction action = MessageActionSetMessagesTTL.Builder()
            .Period(period)
            .Build();
        return action.AsSpan().ToArray();
    }
}
