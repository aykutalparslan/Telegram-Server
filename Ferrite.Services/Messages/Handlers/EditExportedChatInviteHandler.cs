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

public sealed class EditExportedChatInviteHandler : MessagesHandlerBase
{
    private readonly IChatInvitesRepository _chatInvitesRepository;

    public EditExportedChatInviteHandler(IUnitOfWork unitOfWork, IForumTopicsRepository forumTopicsRepository, IMessagingSettingsRepository messagingSettingsRepository, IAuthorizationRepository authorizationRepository, IChannelMessagesRepository channelMessagesRepository, IChatInvitesRepository chatInvitesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,ISearchEngine search,
        IUpdatesService updates, IUpdatesContextFactory updatesContextFactory,
        ILogger log, IUploadService upload, IPhotoProcessingService photos,
        ICounterFactory counterFactory, IdAllocators ids,
        ChatRowStore chatRows, InviteStore invites,
        PrivacyEvaluator privacy, MessageStore messages, SendPipeline send,
        UpdateFanout fanout, DialogBuilder dialogs)
        : base(unitOfWork, forumTopicsRepository, messagingSettingsRepository, authorizationRepository, channelMessagesRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, search, updates, updatesContextFactory, log, upload, photos, counterFactory, ids, chatRows, invites, privacy, messages, send, fanout, dialogs)
    {
        _chatInvitesRepository = chatInvitesRepository;

    }

    [TLFunction(Constructors.baseLayer_EditExportedChatInvite)]
    public async Task<Ferrite.TL.baseLayer.messages.TLExportedChatInvite> Handle(
            long authKeyId, TLBytes q)
        {
            var request = (EditExportedChatInvite)q;
            (bool isChannel, long chatId) = PeerResolver.ResolveInviteChatPeer(request.Get_PeerView());
            bool revoke = request.Revoked;
            string hash = ChatInvites.HashFromLink(Encoding.UTF8.GetString(request.Link));
            bool hasExpireDate = request.Flags[0];
            int expireDate = request.ExpireDate;
            bool hasUsageLimit = request.Flags[1];
            int usageLimit = request.UsageLimit;
            bool hasRequestNeeded = request.Flags[3];
            bool requestNeeded = request.RequestNeeded;
            bool hasTitle = request.Flags[4];
            byte[] title = request.Title.ToArray();

            var (context, error) = await PrepareInviteAdmin(authKeyId, isChannel, chatId);
            if (error != null)
            {
                return ErrorMessagesExportedInvite(error);
            }

            var invite = await _invites.GetStoredInviteAsync(chatId, hash);
            if (invite == null)
            {
                return ErrorMessagesExportedInvite("INVITE_HASH_INVALID");
            }
            if (!context!.IsCreator && invite.AdminId != context.CurrentUserId)
            {
                // Non-creator admins may only edit their own links.
                return ErrorMessagesExportedInvite("CHAT_ADMIN_REQUIRED");
            }

            if (revoke)
            {
                if (invite.Revoked)
                {
                    return ErrorMessagesExportedInvite("INVITE_HASH_EXPIRED");
                }
                _invites.PutStoredInvite(invite, revoked: true, invite.RequestNeeded, invite.ExpireDate,
                    invite.UsageLimit, invite.Usage, invite.Title);
                var revokedRow = await _invites.GetStoredInviteAsync(chatId, hash);
                byte[] revokedBytes = revokedRow!.InviteBytes;

                if (invite.Permanent)
                {
                    // Revoking the primary link replaces it: the server mints a fresh
                    // permanent link and returns both.
                    int date = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
                    string newHash = ChatInvites.GenerateHash();
                    byte[] newInviteBytes;
                    using (TLChatInviteInfo replacement = ChatInvites.BuildInviteInfo(chatId,
                               newHash, context.CurrentUserId, date, permanent: true,
                               revoked: false, requestNeeded: false, expireDate: 0,
                               usageLimit: 0, usage: 0, title: null))
                    {
                        _chatInvitesRepository.PutInvite(replacement);
                        newInviteBytes = replacement.AsChatInviteInfo().Invite.ToArray();
                    }
                    await _unitOfWork.SaveAsync();
                    _log.Debug($"🔗 EditExportedChatInvite user:{context.CurrentUserId} " +
                               $"chat:{chatId} hash:{hash} revoked permanent -> {newHash}");
                    var replacedUsers = new Vector();
                    AppendUsers(ref replacedUsers, new[] { invite.AdminId, context.CurrentUserId });
                    return ExportedChatInviteReplaced.Builder()
                        .Invite(revokedBytes)
                        .NewInvite(newInviteBytes)
                        .Users(replacedUsers)
                        .Build();
                }

                await _unitOfWork.SaveAsync();
                _log.Debug($"🔗 EditExportedChatInvite user:{context.CurrentUserId} " +
                           $"chat:{chatId} hash:{hash} revoked");
                var revokedUsers = new Vector();
                AppendUsers(ref revokedUsers, new[] { invite.AdminId });
                return Ferrite.TL.baseLayer.messages.ExportedChatInvite.Builder()
                    .Invite(revokedBytes)
                    .Users(revokedUsers)
                    .Build();
            }

            if (invite.Revoked)
            {
                // Revoked links can no longer be edited.
                return ErrorMessagesExportedInvite("INVITE_HASH_EXPIRED");
            }

            _invites.PutStoredInvite(invite, revoked: false,
                hasRequestNeeded ? requestNeeded : invite.RequestNeeded,
                hasExpireDate ? expireDate : invite.ExpireDate,
                hasUsageLimit ? usageLimit : invite.UsageLimit,
                invite.Usage,
                hasTitle ? (title.Length > 0 ? title : null) : invite.Title);
            await _unitOfWork.SaveAsync();

            var edited = await _invites.GetStoredInviteAsync(chatId, hash);
            _log.Debug($"🔗 EditExportedChatInvite user:{context.CurrentUserId} chat:{chatId} " +
                       $"hash:{hash} edited");
            var userVector = new Vector();
            AppendUsers(ref userVector, new[] { invite.AdminId });
            return Ferrite.TL.baseLayer.messages.ExportedChatInvite.Builder()
                .Invite(edited!.InviteBytes)
                .Users(userVector)
                .Build();
        }
}
