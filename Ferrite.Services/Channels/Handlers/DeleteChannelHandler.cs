// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using System.Text.RegularExpressions;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.channels;
using Ferrite.TL.baseLayer.dto;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

public sealed class DeleteChannelHandler : ChannelsHandlerBase
{
    private readonly IChannelMessagesRepository _channelMessagesRepository;
    private readonly IChatInvitesRepository _chatInvitesRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IForumTopicsRepository _forumTopicsRepository;

    public DeleteChannelHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChannelMessagesRepository channelMessagesRepository, IChatInvitesRepository chatInvitesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IForumTopicsRepository forumTopicsRepository, IMessageRepository messageRepository, IUserRepository userRepository, ICounterFactory counterFactory,
        IdAllocators ids, IUpdatesContextFactory updatesContextFactory,
        IUpdatesService updates, ISearchEngine search, IUploadService upload,
        IPhotoProcessingService photos, ILogger log, ChatRowStore chatRows,
        UpdateFanout fanout)
        : base(unitOfWork, channelMessagesRepository, authorizationRepository, channelAdminLogRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, counterFactory, ids, updatesContextFactory, updates,
            search, upload, photos, log, chatRows, fanout)
    {
        _channelMessagesRepository = channelMessagesRepository;
        _chatInvitesRepository = chatInvitesRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;
        _forumTopicsRepository = forumTopicsRepository;

    }

    [TLFunction(Constructors.baseLayer_DeleteChannel)]
    public async Task<Ferrite.TL.baseLayer.TLUpdates> Handle(long authKeyId, TLBytes q)
    {
        long? channelId = ResolveInputChannelId(((DeleteChannel)q).Get_ChannelView());

        var (currentUserId, channelBytes, error) =
            await PrepareChannelMutation(authKeyId, channelId, creatorOnly: true);
        if (error != null)
        {
            return error.Value;
        }

        long id = channelId!.Value;
        bool broadcast;
        bool megagroup;
        long accessHash;
        byte[] titleBytes;
        {
            using var stored = new TLChat(channelBytes, 0, channelBytes.Length);
            var channel = stored.AsChannel();
            broadcast = channel.Broadcast;
            megagroup = channel.Megagroup;
            accessHash = channel.AccessHash;
            titleBytes = channel.Title.ToArray();
        }

        string deletedUsername = ReadChannelUsername(channelBytes);
        if (deletedUsername.Length > 0)
        {
            _chatRepository.DeleteUsername(deletedUsername);
            await _search.DeleteChat(id);
        }
        _chatInvitesRepository.DeleteInvites(id);
        _chatInvitesRepository.DeleteImporters(id);
        _chatInvitesRepository.DeletePendingImporters(id);
        _forumTopicsRepository?.DeleteTopics(id);
        _chatParticipantsRepository.DeleteParticipants(id);
        _channelMessagesRepository.DeleteMessages(id);
        _chatRepository.DeleteFullInfo(id);
        _chatRepository.DeleteChat(id);
        await _unitOfWork.SaveAsync();

        int date = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
        var seqCtx = _updatesContextFactory.GetUpdatesContext(authKeyId, currentUserId);
        int seq = await seqCtx.IncrementSeq();

        byte[] forbiddenBytes;
        {
            var forbiddenBuilder = ChannelForbidden.Builder()
                .Id(id)
                .AccessHash(accessHash)
                .Title(titleBytes);
            if (broadcast)
            {
                forbiddenBuilder = forbiddenBuilder.Broadcast(true);
            }
            if (megagroup)
            {
                forbiddenBuilder = forbiddenBuilder.Megagroup(true);
            }
            using TLChat forbidden = forbiddenBuilder.Build();
            forbiddenBytes = forbidden.AsSpan().ToArray();
        }

        var resultUpdates = new Vector();
        using (TLUpdate updateChannel = UpdateChannel.Builder().ChannelId(id).Build())
        {
            resultUpdates.AppendTLObject(updateChannel.AsSpan());
        }
        var chatVector = new Vector();
        chatVector.AppendTLObject(forbiddenBytes);
        var userVector = new Vector();
        AppendUser(ref userVector, currentUserId);

        _log.Debug($"📣 DeleteChannel user:{currentUserId} channel:{id}");
        return Ferrite.TL.baseLayer.Updates.Builder()
            .UpdatesProperty(resultUpdates)
            .Users(userVector)
            .Chats(chatVector)
            .Date(date)
            .Seq(seq)
            .Build();
    }
}
