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

public sealed class GetChatInviteImportersHandler : MessagesHandlerBase
{
    private readonly IChatInvitesRepository _chatInvitesRepository;

    public GetChatInviteImportersHandler(IUnitOfWork unitOfWork, IForumTopicsRepository forumTopicsRepository, IMessagingSettingsRepository messagingSettingsRepository, IAuthorizationRepository authorizationRepository, IChannelMessagesRepository channelMessagesRepository, IChatInvitesRepository chatInvitesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,ISearchEngine search,
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

    [TLFunction(Constructors.baseLayer_GetChatInviteImporters)]
    public async Task<Ferrite.TL.baseLayer.messages.TLChatInviteImporters> Handle(
            long authKeyId, TLBytes q)
        {
            var request = (GetChatInviteImporters)q;
            (bool isChannel, long chatId) = PeerResolver.ResolveInviteChatPeer(request.Get_PeerView());
            bool requested = request.Requested;
            string linkFilter = request.Link.Length > 0
                ? ChatInvites.HashFromLink(Encoding.UTF8.GetString(request.Link))
                : "";
            string query = request.Q.Length > 0 ? Encoding.UTF8.GetString(request.Q) : "";
            int offsetDate = request.OffsetDate;
            (bool offsetIsSelf, long offsetUserId) = PeerResolver.ReadInputUser(request.Get_OffsetUserView());
            int limit = request.Limit;

            var (context, error) = await PrepareInviteAdmin(authKeyId, isChannel, chatId);
            if (error != null)
            {
                return ErrorChatInviteImporters(error);
            }
            long offsetUser = offsetIsSelf ? context!.CurrentUserId : offsetUserId;

            var importers = new List<StoredImporter>();
            if (requested)
            {
                List<PendingInviteImporter> rows = await ChatInvites
                    .GetPendingImportersAsync(_chatInvitesRepository,
                        chatId);
                importers.AddRange(rows.Select(info => new StoredImporter(
                    info.UserId, info.Date, info.Link, info.About,
                    Requested: true)));
            }
            else
            {
                var rows = await _chatInvitesRepository.GetImportersAsync(chatId);
                foreach (var row in rows)
                {
                    using var r = row;
                    var info = r.AsChatInviteImporterInfo();
                    importers.Add(new StoredImporter(info.UserId, info.Date,
                        Encoding.UTF8.GetString(info.Link)));
                }
            }

            var filtered = importers
                .Where(i => linkFilter.Length == 0 || ChatInvites.HashFromLink(i.Link) == linkFilter)
                .Where(i => query.Length == 0 || ImporterMatchesQuery(i.UserId, query))
                .OrderByDescending(i => i.Date)
                .ThenBy(i => i.UserId)
                .ToList();
            if (offsetDate > 0)
            {
                filtered = filtered
                    .Where(i => i.Date < offsetDate ||
                                (i.Date == offsetDate && offsetUser > 0 && i.UserId > offsetUser))
                    .ToList();
            }

            int take = limit > 0 ? Math.Min(limit, 100) : filtered.Count;
            var page = filtered.Take(take).ToList();

            var importersVector = new Vector();
            foreach (var importer in page)
            {
                var builder = ChatInviteImporter.Builder()
                    .Requested(importer.Requested)
                    .UserId(importer.UserId)
                    .Date(importer.Date);
                if (!string.IsNullOrEmpty(importer.About))
                {
                    builder = builder.About(Encoding.UTF8.GetBytes(importer.About));
                }
                using var row = builder.Build();
                importersVector.AppendTLObject(row.ToReadOnlySpan());
            }
            var userVector = new Vector();
            AppendUsers(ref userVector, page.Select(i => i.UserId));

            _log.Debug($"🔗 GetChatInviteImporters user:{context!.CurrentUserId} chat:{chatId} " +
                       $"requested:{requested} total:{filtered.Count} page:{page.Count}");
            return ChatInviteImporters.Builder()
                .Count(filtered.Count)
                .Importers(importersVector)
                .Users(userVector)
                .Build();
        }
}
