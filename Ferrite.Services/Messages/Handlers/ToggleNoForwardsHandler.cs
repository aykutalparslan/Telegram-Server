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
/// Restricts or re-allows saving content in a basic group or channel. A private
/// dialog has no shared row to carry the restriction, and the pinned client
/// refuses to send one for a user peer.
/// </summary>
public sealed class ToggleNoForwardsHandler : MessagesHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;

    public ToggleNoForwardsHandler(IUnitOfWork unitOfWork, IForumTopicsRepository forumTopicsRepository, IMessagingSettingsRepository messagingSettingsRepository, IAuthorizationRepository authorizationRepository, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository, ISearchEngine search,
        IUpdatesService updates, IUpdatesContextFactory updatesContextFactory,
        ILogger log, IUploadService upload, IPhotoProcessingService photos,
        ICounterFactory counterFactory, IdAllocators ids,
        ChatRowStore chatRows, InviteStore invites,
        PrivacyEvaluator privacy, MessageStore messages, SendPipeline send,
        UpdateFanout fanout, DialogBuilder dialogs)
        : base(unitOfWork, forumTopicsRepository, messagingSettingsRepository, authorizationRepository, channelMessagesRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, search, updates, updatesContextFactory, log, upload, photos, counterFactory, ids, chatRows, invites, privacy, messages, send, fanout, dialogs)
    {
        _authorizationRepository = authorizationRepository;

    }

    [TLFunction(Constructors.baseLayer_ToggleNoForwards)]
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

        var request = (ToggleNoForwards)q;
        DialogPeerKey? destination = PeerResolver.ResolveOptionalDialogPeer(
            request.Get_PeerView(), userId);
        bool enabled = request.Enabled;
        if (destination == null || destination.Value.Id <= 0 ||
            destination.Value.Type == TLPeer.PeerType.PeerUser)
        {
            return ErrorUpdates("PEER_ID_INVALID");
        }

        return destination.Value.Type == TLPeer.PeerType.PeerChat
            ? await ToggleBasicGroupAsync(authKeyId, destination.Value.Id, enabled)
            : await ToggleChannelAsync(authKeyId, userId, destination.Value.Id,
                enabled);
    }

    private async Task<TLUpdates> ToggleBasicGroupAsync(long authKeyId, long chatId,
        bool enabled)
    {
        var (context, error) = await PrepareBasicChatMutation(authKeyId, chatId,
            requireAdmin: true, requireCreator: true);
        if (error != null)
        {
            return ErrorUpdates(error);
        }

        try
        {
            byte[] updatedChatBytes = _chatRows.UpdateStoredChatNoforwards(
                context.ChatBytes, enabled);
            if (!await _unitOfWork.SaveAsync())
            {
                return ErrorUpdates("INTERNAL_SERVER_ERROR");
            }

            var participantIds = new List<long>(context.ActiveParticipants.Count);
            foreach (TLChatParticipantInfo participant in context.ActiveParticipants)
            {
                participantIds.Add(participant.AsChatParticipantInfo().UserId);
            }
            await _fanout.PushUpdateChatAsync(chatId, participantIds);

            int date = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
            int seq = await _updatesContextFactory
                .GetUpdatesContext(authKeyId, context.CurrentUserId).IncrementSeq();
            _log.Debug($"🚫 ToggleNoForwards user:{context.CurrentUserId} " +
                       $"chat:{chatId} enabled:{enabled}");
            using TLUpdate updateChat = UpdateChat.Builder().ChatId(chatId).Build();
            return _fanout.BuildUpdates(new[] { updateChat.AsSpan().ToArray() },
                participantIds, new[] { updatedChatBytes }, date, seq);
        }
        finally
        {
            DisposeParticipants(context.ActiveParticipants);
        }
    }

    private async Task<TLUpdates> ToggleChannelAsync(long authKeyId, long userId,
        long channelId, bool enabled)
    {
        var (channelBytes, _, participantBytes, error) =
            await GetChannelInteractionContext(userId, channelId);
        if (error != null)
        {
            return ErrorUpdates(error);
        }
        using (var participant = new TLChatParticipantInfo(participantBytes, 0,
                   participantBytes.Length))
        {
            if (participant.AsChatParticipantInfo().Role !=
                (int)ChatParticipantRole.Creator)
            {
                return ErrorUpdates("CHAT_ADMIN_REQUIRED");
            }
        }

        byte[] updatedChannelBytes = _chatRows.UpdateStoredChannelNoforwards(
            channelBytes, enabled);
        if (!await _unitOfWork.SaveAsync())
        {
            return ErrorUpdates("INTERNAL_SERVER_ERROR");
        }

        await _fanout.PushUpdateChannelToOtherMembersAsync(channelId, userId);

        int date = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
        int seq = await _updatesContextFactory.GetUpdatesContext(authKeyId, userId)
            .IncrementSeq();
        _log.Debug($"🚫 ToggleNoForwards user:{userId} channel:{channelId} " +
                   $"enabled:{enabled}");
        using TLUpdate updateChannel = UpdateChannel.Builder()
            .ChannelId(channelId)
            .Build();
        return _fanout.BuildUpdates(new[] { updateChannel.AsSpan().ToArray() },
            new[] { userId }, new[] { updatedChannelBytes }, date, seq);
    }
}
