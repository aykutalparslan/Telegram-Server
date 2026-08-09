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

namespace Ferrite.Services.Handlers.MessageMethods;

/// <summary>
/// Applies a generated chat wallpaper, clears it, restores a wallpaper that was
/// overridden by the other private participant, or accepts an earlier wallpaper
/// service message. Image and pattern lookup remains owned by the /// account-wallpaper surface; this handler can resolve generated no-file fills
/// without inventing a second wallpaper catalogue.
/// </summary>
public sealed class SetChatWallPaperHandler : MessagesHandlerBase
{
    private readonly IChannelMessagesRepository _channelMessagesRepository;

    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IUserRepository _userRepository;

    private readonly ChatSettingsStore _settings;
    private readonly MessageLocator _locator;
    private readonly TimeProvider _timeProvider;

    public SetChatWallPaperHandler(IUnitOfWork unitOfWork, IForumTopicsRepository forumTopicsRepository, IMessagingSettingsRepository messagingSettingsRepository, IAuthorizationRepository authorizationRepository, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository, ISearchEngine search,
        IUpdatesService updates, IUpdatesContextFactory updatesContextFactory,
        ILogger log, IUploadService upload, IPhotoProcessingService photos,
        ICounterFactory counterFactory, IdAllocators ids,
        ChatRowStore chatRows, InviteStore invites,
        PrivacyEvaluator privacy, MessageStore messages, SendPipeline send,
        UpdateFanout fanout, DialogBuilder dialogs, ChatSettingsStore settings,
        MessageLocator locator, TimeProvider timeProvider)
        : base(unitOfWork, forumTopicsRepository, messagingSettingsRepository, authorizationRepository, channelMessagesRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, search, updates, updatesContextFactory, log, upload,
            photos, counterFactory, ids, chatRows, invites, privacy, messages,
            send, fanout, dialogs)
    {
        _channelMessagesRepository = channelMessagesRepository;

        _authorizationRepository = authorizationRepository;
        _userRepository = userRepository;

        _settings = settings;
        _locator = locator;
        _timeProvider = timeProvider;
    }

    [TLFunction(Constructors.baseLayer_SetChatWallPaper)]
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

        WallpaperOperation operation;
        var request = (SetChatWallPaper)q;
        DialogPeerKey? destination = PeerResolver.ResolveOptionalDialogPeer(
            request.Get_PeerView(), userId);
        bool forBoth = request.ForBoth;
        int sourceMessageId = request.Id;
        bool hasWallpaper = request.Flags[0];
        bool hasSourceMessage = request.Flags[1];
        bool hasSettings = request.Flags[2];
        bool generatedFill = hasWallpaper &&
            request.Get_WallpaperView().Is(out InputWallPaperNoFile _);

        if (request.Revert && !forBoth && !hasWallpaper &&
            !hasSourceMessage && !hasSettings)
        {
            operation = WallpaperOperation.Revert;
        }
        else if (!request.Revert && !forBoth && !hasWallpaper &&
                 hasSourceMessage && sourceMessageId > 0 && hasSettings)
        {
            operation = WallpaperOperation.Acknowledge;
        }
        else if (!request.Revert && hasWallpaper && !hasSourceMessage &&
                 hasSettings)
        {
            operation = WallpaperOperation.Set;
        }
        else if (!request.Revert && !forBoth && !hasWallpaper &&
                 !hasSourceMessage && !hasSettings)
        {
            operation = WallpaperOperation.Delete;
        }
        else
        {
            return ErrorUpdates("WALLPAPER_NOT_FOUND");
        }

        if (destination == null || destination.Value.Id <= 0)
        {
            return ErrorUpdates("PEER_ID_INVALID");
        }
        if (operation == WallpaperOperation.Set && !generatedFill)
        {
            // inputWallPaper/inputWallPaperSlug require the account wallpaper
            // catalogue and upload lifecycle.
            return ErrorUpdates("WALLPAPER_NOT_FOUND");
        }

        return destination.Value.Type switch
        {
            TLPeer.PeerType.PeerUser => await SetPrivateWallpaperAsync(authKeyId,
                userId, destination.Value.Id, operation, forBoth,
                sourceMessageId, q),
            TLPeer.PeerType.PeerChannel => await SetChannelWallpaperAsync(authKeyId,
                userId, destination.Value.Id, operation, forBoth, q),
            _ => ErrorUpdates("PEER_ID_INVALID"),
        };
    }

    private async Task<TLUpdates> SetPrivateWallpaperAsync(long authKeyId,
        long userId, long peerUserId, WallpaperOperation operation, bool forBoth,
        int sourceMessageId, TLBytes requestBytes)
    {
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
        if (forBoth && !IsPremiumUser(userId))
        {
            return ErrorUpdates("PREMIUM_ACCOUNT_REQUIRED");
        }

        return operation switch
        {
            WallpaperOperation.Set => await SetPrivateFillAsync(authKeyId, userId,
                peerUserId, forBoth, requestBytes),
            WallpaperOperation.Acknowledge => await AcknowledgePrivateWallpaperAsync(
                authKeyId, userId, peerUserId, sourceMessageId, requestBytes),
            WallpaperOperation.Delete => await ClearPrivateWallpaperAsync(authKeyId,
                userId, peerUserId, revert: false),
            WallpaperOperation.Revert => await ClearPrivateWallpaperAsync(authKeyId,
                userId, peerUserId, revert: true),
            _ => ErrorUpdates("WALLPAPER_NOT_FOUND"),
        };
    }

    private async Task<TLUpdates> SetPrivateFillAsync(long authKeyId, long userId,
        long peerUserId, bool forBoth, TLBytes requestBytes)
    {
        TLWallPaper? built = BuildGeneratedFill(requestBytes);
        if (built == null)
        {
            return ErrorUpdates("WALLPAPER_NOT_FOUND");
        }
        using TLWallPaper wallpaper = built.Value;

        using TLPeerWallpaper? peerState = forBoth
            ? await _settings.GetPrivateWallpaperAsync(peerUserId, userId)
            : null;
        _settings.PutPrivateWallpaper(userId, peerUserId, forBoth,
            overridden: false, wallpaper.AsSpan());
        if (forBoth)
        {
            ReadOnlySpan<byte> previous = default;
            if (peerState != null)
            {
                PeerWallpaper stored = peerState.Value.AsPeerWallpaper();
                previous = stored.Overridden
                    ? stored.PreviousWallpaper
                    : stored.Wallpaper;
            }
            _settings.PutPrivateWallpaper(peerUserId, userId, forBoth: true,
                overridden: true, wallpaper.AsSpan(), previous);
        }

        return await WritePrivateWallpaperActionAsync(authKeyId, userId,
            peerUserId, wallpaper, same: false, forBoth,
            callerReplyToMsgId: 0, peerReplyToMsgId: 0,
            updatePeerWallpaper: forBoth);
    }

    private async Task<TLUpdates> AcknowledgePrivateWallpaperAsync(long authKeyId,
        long userId, long peerUserId, int sourceMessageId, TLBytes requestBytes)
    {
        IReadOnlyList<StoredMessageLocation> copies = await _locator
            .FindCommonCopiesAsync(userId, sourceMessageId);
        StoredMessageLocation? callerCopy = null;
        StoredMessageLocation? peerCopy = null;
        foreach (StoredMessageLocation copy in copies)
        {
            if (copy.OwnerId == userId && copy.MessageId == sourceMessageId)
            {
                callerCopy = copy;
            }
            else if (copy.OwnerId == peerUserId)
            {
                peerCopy = copy;
            }
        }
        if (callerCopy == null || peerCopy == null ||
            !IsWallpaperMessageInDialog(callerCopy.Value.MessageBytes,
                TLPeer.PeerType.PeerUser, peerUserId))
        {
            return ErrorUpdates("MESSAGE_ID_INVALID");
        }

        TLWallPaper? built = BuildAcknowledgedWallpaper(
            callerCopy.Value.MessageBytes, requestBytes);
        if (built == null)
        {
            return ErrorUpdates("WALLPAPER_NOT_FOUND");
        }
        using TLWallPaper wallpaper = built.Value;
        _settings.PutPrivateWallpaper(userId, peerUserId, forBoth: false,
            overridden: false, wallpaper.AsSpan());

        return await WritePrivateWallpaperActionAsync(authKeyId, userId,
            peerUserId, wallpaper, same: true, forBoth: false,
            callerReplyToMsgId: sourceMessageId,
            peerReplyToMsgId: peerCopy.Value.MessageId,
            updatePeerWallpaper: false);
    }

    private async Task<TLUpdates> WritePrivateWallpaperActionAsync(long authKeyId,
        long userId, long peerUserId, TLWallPaper wallpaper, bool same,
        bool forBoth, int callerReplyToMsgId, int peerReplyToMsgId,
        bool updatePeerWallpaper)
    {
        byte[] actionBytes = BuildAction(wallpaper.AsSpan(), same, forBoth);
        int date = UnixNow();
        StoredMessageWrite callerWrite = await _messages.PutPrivateServiceMessageAsync(
            userId, authKeyId, peerUserId, userId, outgoing: true, actionBytes, date,
            BuildReplyToHeader(callerReplyToMsgId));
        StoredMessageWrite peerWrite = await _messages.PutPrivateServiceMessageAsync(
            peerUserId, null, userId, userId, outgoing: false, actionBytes, date,
            BuildReplyToHeader(peerReplyToMsgId));
        long logicalId = await _messages.CreateMessageCopyAsync(userId,
            callerWrite.Id);
        _messages.PutMessageCopy(logicalId, peerUserId, peerWrite.Id);
        if (!await _unitOfWork.SaveAsync())
        {
            return ErrorUpdates("INTERNAL_SERVER_ERROR");
        }

        await _fanout.EnqueueNewMessageAsync(peerUserId, peerWrite.Bytes,
            peerWrite.Pts);
        if (updatePeerWallpaper)
        {
            byte[] peerUpdate = BuildWallpaperUpdateBytes(
                TLPeer.PeerType.PeerUser, userId, wallpaper.AsSpan(),
                overridden: true);
            await _fanout.EnqueueSerializedAsync(peerUserId, peerUpdate);
        }

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
        updateBytes.Add(BuildWallpaperUpdateBytes(TLPeer.PeerType.PeerUser,
            peerUserId, wallpaper.AsSpan(), overridden: false));

        _log.Debug($"🖼️ SetChatWallPaper user:{userId} peer:{peerUserId} " +
                   $"same:{same} forBoth:{forBoth}");
        return _fanout.BuildUpdates(updateBytes, new[] { userId, peerUserId },
            Array.Empty<byte[]>(), date, seq);
    }

    private async Task<TLUpdates> ClearPrivateWallpaperAsync(long authKeyId,
        long userId, long peerUserId, bool revert)
    {
        using TLPeerWallpaper? stored = await _settings
            .GetPrivateWallpaperAsync(userId, peerUserId);
        ReadOnlySpan<byte> restored = default;
        if (revert)
        {
            if (stored == null || !stored.Value.AsPeerWallpaper().Overridden)
            {
                return ErrorUpdates("WALLPAPER_NOT_FOUND");
            }
            PeerWallpaper row = stored.Value.AsPeerWallpaper();
            restored = row.PreviousWallpaper;
            if (row.Flags[3])
            {
                _settings.PutPrivateWallpaper(userId, peerUserId,
                    forBoth: false, overridden: false, restored);
            }
            else
            {
                _settings.DeletePrivateWallpaper(userId, peerUserId);
            }
        }
        else
        {
            _settings.DeletePrivateWallpaper(userId, peerUserId);
        }

        byte[] updateBytes = BuildWallpaperUpdateBytes(
            TLPeer.PeerType.PeerUser, peerUserId, restored, overridden: false);
        if (!await _unitOfWork.SaveAsync())
        {
            return ErrorUpdates("INTERNAL_SERVER_ERROR");
        }
        int seq = await _updatesContextFactory.GetUpdatesContext(authKeyId, userId)
            .IncrementSeq();
        _log.Debug($"🖼️ SetChatWallPaper user:{userId} peer:{peerUserId} " +
                   (revert ? "revert" : "delete"));
        return _fanout.BuildUpdates(new[] { updateBytes },
            new[] { userId, peerUserId }, Array.Empty<byte[]>(), UnixNow(), seq);
    }

    private async Task<TLUpdates> SetChannelWallpaperAsync(long authKeyId,
        long userId, long channelId, WallpaperOperation operation, bool forBoth,
        TLBytes requestBytes)
    {
        if (forBoth || operation is WallpaperOperation.Acknowledge or
            WallpaperOperation.Revert)
        {
            return ErrorUpdates("WALLPAPER_NOT_FOUND");
        }

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

        int date = UnixNow();
        if (operation == WallpaperOperation.Delete)
        {
            _settings.DeleteChannelWallpaper(channelId);
            byte[] cleared = BuildWallpaperUpdateBytes(
                TLPeer.PeerType.PeerChannel, channelId, default,
                overridden: false);
            if (!await _unitOfWork.SaveAsync())
            {
                return ErrorUpdates("INTERNAL_SERVER_ERROR");
            }
            await _fanout.PushSerializedToOtherChannelMembersAsync(channelId,
                userId, new[] { cleared });
            int deleteSeq = await _updatesContextFactory
                .GetUpdatesContext(authKeyId, userId).IncrementSeq();
            return _fanout.BuildUpdates(new[] { cleared }, new[] { userId },
                new[] { channelBytes }, date, deleteSeq);
        }

        TLWallPaper? built = BuildGeneratedFill(requestBytes);
        if (operation != WallpaperOperation.Set || built == null)
        {
            return ErrorUpdates("WALLPAPER_NOT_FOUND");
        }
        using TLWallPaper wallpaper = built.Value;
        _settings.PutChannelWallpaper(channelId, wallpaper.AsSpan());

        byte[] actionBytes = BuildAction(wallpaper.AsSpan(), same: false,
            forBoth: false);
        StoredMessageWrite write = await MessageStore.PutChannelServiceMessageAsync(_channelMessagesRepository, _counterFactory, channelId, userId, actionBytes, date);
        if (!await _unitOfWork.SaveAsync())
        {
            return ErrorUpdates("INTERNAL_SERVER_ERROR");
        }

        byte[] wallpaperUpdate = BuildWallpaperUpdateBytes(
            TLPeer.PeerType.PeerChannel, channelId, wallpaper.AsSpan(),
            overridden: false);
        await _fanout.PushChannelServiceMessageAsync(channelId, userId, write.Bytes,
            write.Pts);
        await _fanout.PushSerializedToOtherChannelMembersAsync(channelId, userId,
            new[] { wallpaperUpdate });

        int seq = await _updatesContextFactory.GetUpdatesContext(authKeyId, userId)
            .IncrementSeq();
        var updateBytes = new List<byte[]>(2);
        using (TLUpdate updateNewMessage = UpdateNewChannelMessage.Builder()
                   .Message(write.Bytes)
                   .Pts(write.Pts)
                   .PtsCount(1)
                   .Build())
        {
            updateBytes.Add(updateNewMessage.AsSpan().ToArray());
        }
        updateBytes.Add(wallpaperUpdate);
        _log.Debug($"🖼️ SetChatWallPaper user:{userId} channel:{channelId}");
        return _fanout.BuildUpdates(updateBytes, new[] { userId },
            new[] { channelBytes }, date, seq);
    }

    private bool IsPremiumUser(long userId)
    {
        using TLUser? user = _userRepository.GetUser(userId);
        return user != null && user.Value.Type == TLUser.UserType.User &&
               user.Value.AsUser().Premium;
    }

    private static bool IsWallpaperMessageInDialog(byte[] messageBytes,
        TLPeer.PeerType peerType, long peerId)
    {
        using var message = new TLMessage(messageBytes, 0, messageBytes.Length);
        return message.Type == TLMessage.MessageType.MessageService &&
               MessageStore.TryReadStoredMessageInfo(message, out var info) &&
               info.PeerType == peerType && info.PeerId == peerId &&
               message.AsMessageService().Get_ActionView()
                   .Is(out MessageActionSetChatWallPaper _);
    }

    private static TLWallPaper? BuildGeneratedFill(TLBytes requestBytes)
    {
        var request = (SetChatWallPaper)requestBytes;
        if (!request.Get_WallpaperView().Is(out InputWallPaperNoFile input) ||
            !request.Get_SettingsView().Is(out WallPaperSettings settings))
        {
            return null;
        }
        using TLWallPaperSettings ownedSettings = settings.Clone().Build();
        return WallPaperNoFile.Builder()
            .Id(input.Id)
            .Settings(ownedSettings.AsSpan())
            .Build();
    }

    private static TLWallPaper? BuildAcknowledgedWallpaper(byte[] messageBytes,
        TLBytes requestBytes)
    {
        var request = (SetChatWallPaper)requestBytes;
        if (!request.Get_SettingsView().Is(out WallPaperSettings settings))
        {
            return null;
        }
        using var message = new TLMessage(messageBytes, 0, messageBytes.Length);
        if (message.Type != TLMessage.MessageType.MessageService ||
            !message.AsMessageService().Get_ActionView()
                .Is(out MessageActionSetChatWallPaper action))
        {
            return null;
        }

        using TLWallPaperSettings ownedSettings = settings.Clone().Build();
        WallPaperView wallpaper = action.Get_WallpaperView();
        if (wallpaper.Is(out WallPaperNoFile noFile))
        {
            return noFile.Clone().Settings(ownedSettings.AsSpan()).Build();
        }
        if (wallpaper.Is(out WallPaper file))
        {
            return file.Clone().Settings(ownedSettings.AsSpan()).Build();
        }
        return null;
    }

    private static byte[] BuildAction(ReadOnlySpan<byte> wallpaper, bool same,
        bool forBoth)
    {
        using TLMessageAction action = MessageActionSetChatWallPaper.Builder()
            .Same(same)
            .ForBoth(forBoth)
            .Wallpaper(wallpaper)
            .Build();
        return action.AsSpan().ToArray();
    }

    private static byte[]? BuildReplyToHeader(int replyToMsgId)
    {
        if (replyToMsgId <= 0)
        {
            return null;
        }
        using TLMessageReplyHeader replyTo = MessageReplyHeader.Builder()
            .ReplyToMsgId(replyToMsgId)
            .Build();
        return replyTo.AsSpan().ToArray();
    }

    private static byte[] BuildWallpaperUpdateBytes(TLPeer.PeerType peerType,
        long peerId, ReadOnlySpan<byte> wallpaper, bool overridden)
    {
        using TLPeer peer = PeerResolver.BuildPeer(peerType, peerId);
        var builder = UpdatePeerWallpaper.Builder()
            .WallpaperOverridden(overridden)
            .Peer(peer.AsSpan());
        if (!wallpaper.IsEmpty)
        {
            builder = builder.Wallpaper(wallpaper);
        }
        using TLUpdate update = builder.Build();
        return update.AsSpan().ToArray();
    }

    private int UnixNow() =>
        checked((int)_timeProvider.GetUtcNow().ToUnixTimeSeconds());

    private enum WallpaperOperation
    {
        Set,
        Acknowledge,
        Delete,
        Revert,
    }
}
