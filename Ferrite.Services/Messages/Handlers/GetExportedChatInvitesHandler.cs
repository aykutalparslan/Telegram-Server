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

public sealed class GetExportedChatInvitesHandler : MessagesHandlerBase
{
    public GetExportedChatInvitesHandler(IUnitOfWork unitOfWork, IForumTopicsRepository forumTopicsRepository, IMessagingSettingsRepository messagingSettingsRepository, IAuthorizationRepository authorizationRepository, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,ISearchEngine search,
        IUpdatesService updates, IUpdatesContextFactory updatesContextFactory,
        ILogger log, IUploadService upload, IPhotoProcessingService photos,
        ICounterFactory counterFactory, IdAllocators ids,
        ChatRowStore chatRows, InviteStore invites,
        PrivacyEvaluator privacy, MessageStore messages, SendPipeline send,
        UpdateFanout fanout, DialogBuilder dialogs)
        : base(unitOfWork, forumTopicsRepository, messagingSettingsRepository, authorizationRepository, channelMessagesRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, search, updates, updatesContextFactory, log, upload, photos, counterFactory, ids, chatRows, invites, privacy, messages, send, fanout, dialogs)
    {
    }

    [TLFunction(Constructors.baseLayer_GetExportedChatInvites)]
    public async Task<Ferrite.TL.baseLayer.messages.TLExportedChatInvites> Handle(
            long authKeyId, TLBytes q)
        {
            var request = (GetExportedChatInvites)q;
            (bool isChannel, long chatId) = PeerResolver.ResolveInviteChatPeer(request.Get_PeerView());
            bool revokedFilter = request.Revoked;
            bool hasOffset = request.Flags[2];
            int offsetDate = request.OffsetDate;
            string offsetLink = request.OffsetLink.Length > 0
                ? Encoding.UTF8.GetString(request.OffsetLink)
                : "";
            int limit = request.Limit;
            (bool adminIsSelf, long adminUserId) = PeerResolver.ReadInputUser(request.Get_AdminIdView());

            var (context, error) = await PrepareInviteAdmin(authKeyId, isChannel, chatId);
            if (error != null)
            {
                return ErrorExportedInvites(error);
            }

            long adminFilter = adminIsSelf || adminUserId <= 0 ? context!.CurrentUserId : adminUserId;
            if (adminFilter != context!.CurrentUserId && !context.IsCreator)
            {
                // Non-creator admins may only list their own links.
                return ErrorExportedInvites("CHAT_ADMIN_REQUIRED");
            }

            var filtered = (await _invites.GetStoredInvitesAsync(chatId))
                .Where(i => i.AdminId == adminFilter && i.Revoked == revokedFilter)
                .OrderByDescending(i => i.Date)
                .ThenBy(i => i.Hash, StringComparer.Ordinal)
                .ToList();

            int startIndex = 0;
            if (hasOffset)
            {
                string offsetHash = offsetLink.Length > 0 ? ChatInvites.HashFromLink(offsetLink) : "";
                int offsetIndex = offsetHash.Length > 0
                    ? filtered.FindIndex(i => i.Hash == offsetHash)
                    : -1;
                if (offsetIndex >= 0)
                {
                    startIndex = offsetIndex + 1;
                }
                else
                {
                    int nextIndex = filtered.FindIndex(i => i.Date < offsetDate);
                    startIndex = nextIndex >= 0 ? nextIndex : filtered.Count;
                }
            }

            int take = limit > 0 ? Math.Min(limit, 100) : filtered.Count;
            var page = filtered.Skip(startIndex).Take(take).ToList();

            var invitesVector = new Vector();
            foreach (var invite in page)
            {
                invitesVector.AppendTLObject(invite.InviteBytes);
            }
            var userVector = new Vector();
            AppendUsers(ref userVector, page.Select(i => i.AdminId).Append(adminFilter));

            _log.Debug($"🔗 GetExportedChatInvites user:{context.CurrentUserId} chat:{chatId} " +
                       $"revoked:{revokedFilter} total:{filtered.Count} page:{page.Count}");
            return ExportedChatInvites.Builder()
                .Count(filtered.Count)
                .Invites(invitesVector)
                .Users(userVector)
                .Build();
        }
}
