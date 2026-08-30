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

public sealed class ImportChatInviteHandler : MessagesHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatInvitesRepository _chatInvitesRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IUserRepository _userRepository;

    public ImportChatInviteHandler(IUnitOfWork unitOfWork, IForumTopicsRepository forumTopicsRepository, IMessagingSettingsRepository messagingSettingsRepository, IAuthorizationRepository authorizationRepository, IChannelMessagesRepository channelMessagesRepository, IChatInvitesRepository chatInvitesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,ISearchEngine search,
        IUpdatesService updates, IUpdatesContextFactory updatesContextFactory,
        ILogger log, IUploadService upload, IPhotoProcessingService photos,
        ICounterFactory counterFactory, IdAllocators ids,
        ChatRowStore chatRows, InviteStore invites,
        PrivacyEvaluator privacy, MessageStore messages, SendPipeline send,
        UpdateFanout fanout, DialogBuilder dialogs)
        : base(unitOfWork, forumTopicsRepository, messagingSettingsRepository, authorizationRepository, channelMessagesRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, search, updates, updatesContextFactory, log, upload, photos, counterFactory, ids, chatRows, invites, privacy, messages, send, fanout, dialogs)
    {
        _authorizationRepository = authorizationRepository;
        _chatInvitesRepository = chatInvitesRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;
        _userRepository = userRepository;

    }

    [TLFunction(Constructors.baseLayer_ImportChatInvite)]
    public async Task<TLUpdates> Handle(long authKeyId, TLBytes q)
    {
            var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
            if (auth == null)
            {
                return ErrorUpdates("AUTH_KEY_INVALID");
            }

            long currentUserId = auth.Value.AsAuthInfo().UserId;
            string hash = Encoding.UTF8.GetString(((ImportChatInvite)q).Hash);
            var invite = _invites.GetStoredInviteByHash(hash);
            if (invite == null)
            {
                return ErrorUpdates("INVITE_HASH_INVALID");
            }
            int now = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
            if (invite.Revoked || (invite.ExpireDate > 0 && invite.ExpireDate <= now))
            {
                return ErrorUpdates("INVITE_HASH_EXPIRED");
            }
            if (invite.UsageLimit > 0 && invite.Usage >= invite.UsageLimit)
            {
                return ErrorUpdates("USERS_TOO_MUCH");
            }

            bool isChannel;
            byte[] chatBytes;
            {
                using var chat = await _chatRepository.GetChatAsync(invite.ChatId);
                if (chat == null)
                {
                    return ErrorUpdates("INVITE_HASH_EXPIRED");
                }
                isChannel = chat.Value.Type == TLChat.ChatType.Channel;
                if (!isChannel && (chat.Value.Type != TLChat.ChatType.Chat ||
                                   chat.Value.AsChat().Deactivated))
                {
                    return ErrorUpdates("INVITE_HASH_EXPIRED");
                }
                chatBytes = chat.Value.AsSpan().ToArray();
            }

            var existing = await _chatParticipantsRepository
                .GetParticipantAsync(invite.ChatId, currentUserId);
            bool alreadyActive = existing != null && IsActiveParticipant(existing.Value);
            bool kicked = existing != null &&
                existing.Value.AsChatParticipantInfo().Role == (int)ChatParticipantRole.Banned;
            existing?.Dispose();
            if (kicked)
            {
                return ErrorUpdates("USER_BANNED_IN_CHANNEL");
            }
            if (alreadyActive)
            {
                return ErrorUpdates("USER_ALREADY_PARTICIPANT");
            }

            int date = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
            if (invite.RequestNeeded)
            {
                using TLPendingChatInviteImporter? existingRequest = await _chatInvitesRepository.GetPendingImporterAsync(invite.ChatId,
                        currentUserId);
                if (existingRequest == null)
                {
                    string about = _userRepository.GetAbout(currentUserId)
                        ?? "";
                    using TLPendingChatInviteImporter pending =
                        PendingChatInviteImporter.Builder()
                            .ChatId(invite.ChatId)
                            .UserId(currentUserId)
                            .Date(date)
                            .Link(Encoding.UTF8.GetBytes(
                                ChatInvites.LinkFromHash(invite.Hash)))
                            .About(Encoding.UTF8.GetBytes(about))
                            .Build();
                    _chatInvitesRepository.PutPendingImporter(pending);
                    _invites.PutStoredInvite(invite, invite.Revoked,
                        invite.RequestNeeded, invite.ExpireDate, invite.UsageLimit,
                        invite.Usage, invite.Title,
                        requested: invite.Requested < int.MaxValue
                            ? invite.Requested + 1
                            : int.MaxValue);
                    if (!await _unitOfWork.SaveAsync())
                    {
                        return ErrorUpdates("INTERNAL_SERVER_ERROR");
                    }
                    await NotifyPendingInviteAdminsAsync(invite.ChatId, isChannel);
                }
                return ErrorUpdates("INVITE_REQUEST_SENT");
            }

            _invites.PutStoredInvite(invite, invite.Revoked, invite.RequestNeeded, invite.ExpireDate,
                invite.UsageLimit, invite.Usage + 1, invite.Title);
            using (TLChatInviteImporterInfo importer = ChatInviteImporterInfo.Builder()
                       .ChatId(invite.ChatId)
                       .UserId(currentUserId)
                       .Date(date)
                       .Link(Encoding.UTF8.GetBytes(ChatInvites.LinkFromHash(invite.Hash)))
                       .Build())
            {
                _chatInvitesRepository.PutImporter(importer);
            }

            _log.Debug($"🔗 ImportChatInvite user:{currentUserId} chat:{invite.ChatId} " +
                       $"hash:{hash} channel:{isChannel}");
            return isChannel
                ? await ImportChannelInvite(authKeyId, currentUserId, invite.ChatId,
                    invite.AdminId, chatBytes, date)
                : await ImportBasicChatInvite(authKeyId, currentUserId, invite.ChatId,
                    invite.AdminId, chatBytes, date);
    }

    private async Task NotifyPendingInviteAdminsAsync(long chatId, bool isChannel)
    {
        List<PendingInviteImporter> pending = await ChatInvites
            .GetPendingImportersAsync(_chatInvitesRepository, chatId);
        var recent = new VectorOfLong();
        foreach (long userId in pending.OrderByDescending(x => x.Date)
                     .ThenBy(x => x.UserId).Take(3).Select(x => x.UserId))
        {
            recent.Append(userId);
        }
        using TLPeer peer = PeerResolver.BuildPeer(isChannel
            ? TLPeer.PeerType.PeerChannel
            : TLPeer.PeerType.PeerChat, chatId);
        byte[] updateBytes;
        using (TLUpdate update = UpdatePendingJoinRequests.Builder()
                   .Peer(peer.AsSpan())
                   .RequestsPending(pending.Count)
                   .RecentRequesters(recent)
                   .Build())
        {
            updateBytes = update.AsSpan().ToArray();
        }

        IReadOnlyCollection<TLChatParticipantInfo> participants = await _chatParticipantsRepository.GetParticipantsAsync(chatId);
        var adminIds = new List<long>();
        foreach (TLChatParticipantInfo participant in participants)
        {
            using (participant)
            {
                if (!IsActiveParticipant(participant))
                {
                    continue;
                }
                var info = participant.AsChatParticipantInfo();
                bool canInvite = isChannel
                    ? ChatRights.HasAdminRight(participant,
                        ChatAdminRightRequirement.InviteUsers)
                    : info.Role is (int)ChatParticipantRole.Creator or
                        (int)ChatParticipantRole.Admin;
                if (canInvite)
                {
                    adminIds.Add(info.UserId);
                }
            }
        }
        await _fanout.EnqueueSerializedAsync(adminIds, new[] { updateBytes });
    }
}
