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

public sealed class SetChatThemeHandler : MessagesHandlerBase
{
    private readonly IChannelMessagesRepository _channelMessagesRepository;

    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IUserRepository _userRepository;

    private readonly ChatSettingsStore _settings;
    private readonly TimeProvider _timeProvider;

    public SetChatThemeHandler(IUnitOfWork unitOfWork, IForumTopicsRepository forumTopicsRepository, IMessagingSettingsRepository messagingSettingsRepository, IAuthorizationRepository authorizationRepository, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository, ISearchEngine search,
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

    [TLFunction(Constructors.baseLayer_SetChatTheme)]
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

        var request = (SetChatTheme)q;
        DialogPeerKey? destination = PeerResolver.ResolveOptionalDialogPeer(
            request.Get_PeerView(), userId);
        InputChatThemeView theme = request.Get_ThemeView();
        if (theme.Is(out InputChatThemeUniqueGift _))
        {
            return (TLUpdates)RpcErrorGenerator.GenerateError(403,
                Encoding.UTF8.GetBytes("METHOD_DISABLED"));
        }
        string? emoticon = theme.Is(out InputChatTheme named)
            ? Encoding.UTF8.GetString(named.Emoticon)
            : null;
        if (destination == null || destination.Value.Id <= 0)
        {
            return ErrorUpdates("PEER_ID_INVALID");
        }

        return destination.Value.Type switch
        {
            TLPeer.PeerType.PeerUser => await SetPrivateThemeAsync(authKeyId, userId,
                destination.Value.Id, emoticon),
            TLPeer.PeerType.PeerChat => await SetBasicGroupThemeAsync(authKeyId,
                destination.Value.Id, emoticon),
            TLPeer.PeerType.PeerChannel => await SetChannelThemeAsync(authKeyId,
                userId, destination.Value.Id, emoticon),
            _ => ErrorUpdates("PEER_ID_INVALID"),
        };
    }

    private async Task<TLUpdates> SetPrivateThemeAsync(long authKeyId, long userId,
        long peerUserId, string? emoticon)
    {
        using (TLUser? peer = _userRepository.GetUser(peerUserId))
        {
            if (peer == null || peer.Value.Type != TLUser.UserType.User)
            {
                return ErrorUpdates("PEER_ID_INVALID");
            }
        }

        ChatSettingsScope scope = ChatSettingsScope.ForPrivatePair(userId, peerUserId);
        ChatSettingsSnapshot current = await _settings.GetAsync(scope);
        _settings.Put(scope, current with { ThemeEmoticon = emoticon });

        byte[] actionBytes = BuildAction(emoticon);
        int date = checked((int)_timeProvider.GetUtcNow().ToUnixTimeSeconds());
        StoredMessageWrite callerWrite = await _messages.PutPrivateServiceMessageAsync(
            userId, authKeyId, peerUserId, userId, outgoing: true, actionBytes, date);
        StoredMessageWrite peerWrite = userId == peerUserId
            ? callerWrite
            : await _messages.PutPrivateServiceMessageAsync(peerUserId, null, userId,
                userId, outgoing: false, actionBytes, date);
        if (userId != peerUserId)
        {
            long logicalId = await _messages.CreateMessageCopyAsync(userId,
                callerWrite.Id);
            _messages.PutMessageCopy(logicalId, peerUserId, peerWrite.Id);
        }
        if (!await _unitOfWork.SaveAsync())
        {
            return ErrorUpdates("INTERNAL_SERVER_ERROR");
        }

        if (userId != peerUserId)
        {
            await _fanout.EnqueueNewMessageAsync(peerUserId, peerWrite.Bytes,
                peerWrite.Pts);
        }

        int seq = await _updatesContextFactory.GetUpdatesContext(authKeyId, userId)
            .IncrementSeq();
        var updateBytes = new List<byte[]>(1);
        using (TLUpdate updateNewMessage = UpdateNewMessage.Builder()
                   .Message(callerWrite.Bytes)
                   .Pts(callerWrite.Pts)
                   .PtsCount(1)
                   .Build())
        {
            updateBytes.Add(updateNewMessage.AsSpan().ToArray());
        }

        _log.Debug($"🎨 SetChatTheme user:{userId} peer:{peerUserId} " +
                   $"theme:{emoticon ?? "(none)"}");
        return _fanout.BuildUpdates(userId, updateBytes, new[] { userId, peerUserId },
            Array.Empty<byte[]>(), date, seq);
    }

    private async Task<TLUpdates> SetBasicGroupThemeAsync(long authKeyId, long chatId,
        string? emoticon)
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
            _settings.Put(scope, current with { ThemeEmoticon = emoticon });

            _log.Debug($"🎨 SetChatTheme user:{context.CurrentUserId} chat:{chatId} " +
                       $"theme:{emoticon ?? "(none)"}");
            return await EmitBasicGroupServiceUpdates(authKeyId, context.CurrentUserId,
                chatId, context.ActiveParticipants, BuildAction(emoticon),
                context.ChatBytes);
        }
        finally
        {
            DisposeParticipants(context.ActiveParticipants);
        }
    }

    private async Task<TLUpdates> SetChannelThemeAsync(long authKeyId, long userId,
        long channelId, string? emoticon)
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
        _settings.Put(scope, current with { ThemeEmoticon = emoticon });

        int date = checked((int)_timeProvider.GetUtcNow().ToUnixTimeSeconds());
        StoredMessageWrite write = await MessageStore.PutChannelServiceMessageAsync(_channelMessagesRepository, _counterFactory, channelId, userId, BuildAction(emoticon),
            date);
        if (!await _unitOfWork.SaveAsync())
        {
            return ErrorUpdates("INTERNAL_SERVER_ERROR");
        }

        await _fanout.PushChannelServiceMessageAsync(channelId, userId, write.Bytes,
            write.Pts);

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
        using (TLUpdate updateChannel = UpdateChannel.Builder()
                   .ChannelId(channelId)
                   .Build())
        {
            updateBytes.Add(updateChannel.AsSpan().ToArray());
        }

        _log.Debug($"🎨 SetChatTheme user:{userId} channel:{channelId} " +
                   $"theme:{emoticon ?? "(none)"}");
        return _fanout.BuildUpdates(userId, updateBytes, new[] { userId },
            new[] { channelBytes }, date, seq);
    }

    private static byte[] BuildAction(string? emoticon)
    {
        using TLChatTheme theme = ChatTheme.Builder()
            .Emoticon(Encoding.UTF8.GetBytes(emoticon ?? string.Empty))
            .Build();
        using TLMessageAction action = MessageActionSetChatTheme.Builder()
            .Theme(theme.AsSpan())
            .Build();
        return action.AsSpan().ToArray();
    }
}
