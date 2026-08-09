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

public sealed class SetTypingHandler : MessagesHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;

    public SetTypingHandler(IUnitOfWork unitOfWork, IForumTopicsRepository forumTopicsRepository, IMessagingSettingsRepository messagingSettingsRepository, IAuthorizationRepository authorizationRepository, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,ISearchEngine search,
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

    [TLFunction(Constructors.baseLayer_SetTyping)]
    public async ValueTask<TLBool> Handle(long authKeyId, TLBytes q)
        {
            // Resolve the target user from the span-backed view BEFORE the await; a ref-struct
            // view cannot survive an await. The Action bytes are re-read from a fresh (SetTyping)q
            // cast afterwards (q's memory outlives the call).
            var peer = ((SetTyping)q).Get_PeerView();
            bool toUser = peer.Is(out InputPeerUser targetUser);
            bool toChat = peer.Is(out InputPeerChat targetChat);
            long targetUserId = toUser ? targetUser.UserId : 0;
            long targetChatId = toChat ? targetChat.ChatId : 0;
            long targetChannelId = PeerResolver.ResolveInputPeerChannelId(peer);
            var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
            if (auth == null)
            {
                return ErrorBool("AUTH_KEY_INVALID");
            }

            long currentUserId = auth.Value.AsAuthInfo().UserId;
            if (targetChannelId > 0)
            {
                var (_, broadcast, channelParticipantBytes, channelError) =
                    await GetChannelInteractionContext(currentUserId, targetChannelId);
                if (channelError != null)
                {
                    return ErrorBool(channelError);
                }
                // Typing signals an upcoming post, so broadcast channels gate it on the
                // same post rights as the send path.
                if (broadcast && !ChatRights.HasAdminRight(channelParticipantBytes,
                        ChatAdminRightRequirement.PostMessages))
                {
                    return ErrorBool("CHAT_WRITE_FORBIDDEN");
                }

                var memberIds = await _fanout.GetOtherActiveChannelMemberIdsAsync(
                    targetChannelId, currentUserId);
                byte[] updateBytes;
                using (TLPeer from = PeerUser.Builder().UserId(currentUserId).Build())
                using (TLUpdate channelTypingUpdate = UpdateChannelUserTyping.Builder()
                           .ChannelId(targetChannelId)
                           .FromId(from.AsSpan())
                           .Action(((SetTyping)q).Action)
                           .Build())
                {
                    updateBytes = channelTypingUpdate.AsSpan().ToArray();
                }
                foreach (long memberId in memberIds)
                {
                    await _updates.EnqueueUpdate(memberId,
                        new TLUpdate(updateBytes, 0, updateBytes.Length));
                }

                return BoolTrue.Builder().Build();
            }

            if (toChat)
            {
                var (context, error) = await PrepareBasicChatMutation(authKeyId, targetChatId,
                    requireAdmin: false);
                if (error != null)
                {
                    return ErrorBool(error);
                }

                try
                {
                    using TLPeer from = PeerUser.Builder().UserId(currentUserId).Build();
                    using TLUpdate chatTypingUpdate = UpdateChatUserTyping.Builder()
                        .ChatId(targetChatId)
                        .FromId(from.AsSpan())
                        .Action(((SetTyping)q).Action)
                        .Build();
                    byte[] updateBytes = chatTypingUpdate.AsSpan().ToArray();
                    foreach (var participantInfo in context.ActiveParticipants)
                    {
                        long participantId = participantInfo.AsChatParticipantInfo().UserId;
                        if (participantId != currentUserId)
                        {
                            await _updates.EnqueueUpdate(participantId,
                                new TLUpdate(updateBytes, 0, updateBytes.Length));
                        }
                    }

                    return BoolTrue.Builder().Build();
                }
                finally
                {
                    DisposeParticipants(context.ActiveParticipants);
                }
            }

            if (!toUser)
            {
                return ErrorBool("PEER_ID_INVALID");
            }

            TLUpdate update = UpdateUserTyping.Builder()
                .UserId(currentUserId)
                .Action(((SetTyping)q).Action)
                .Build();
            await _updates.EnqueueUpdate(targetUserId, update);
            return BoolTrue.Builder().Build();
        }
}
