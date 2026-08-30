// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using System.Text.RegularExpressions;
using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.channels;
using Ferrite.TL.baseLayer.dto;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

public sealed class DeleteHistoryHandler : ChannelsHandlerBase
{
    private readonly IChannelMessagesRepository _channelMessagesRepository;

    public DeleteHistoryHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChannelMessagesRepository channelMessagesRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository, ICounterFactory counterFactory,
        IdAllocators ids, IUpdatesContextFactory updatesContextFactory,
        IUpdatesService updates, ISearchEngine search, IUploadService upload,
        IPhotoProcessingService photos, ILogger log, ChatRowStore chatRows,
        UpdateFanout fanout)
        : base(unitOfWork, channelMessagesRepository, authorizationRepository, channelAdminLogRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, counterFactory, ids, updatesContextFactory, updates,
            search, upload, photos, log, chatRows, fanout)
    {
        _channelMessagesRepository = channelMessagesRepository;

    }

    [TLFunction(Constructors.baseLayer_ChannelsDeleteHistory)]
    public async Task<Ferrite.TL.baseLayer.TLUpdates> Handle(long authKeyId, TLBytes q)
    {
        var request = (ChannelsDeleteHistory)q;
        long? channelId = ResolveInputChannelId(request.Get_ChannelView());
        int maxId = request.MaxId;

        var (currentUserId, channelBytes, error) = await PrepareChannelMutation(authKeyId,
            channelId, creatorOnly: false, ChatAdminRightRequirement.DeleteMessages);
        if (error != null)
        {
            return error.Value;
        }

        long id = channelId!.Value;
        var channelBox = new ChannelMessageBox(_counterFactory, id);

        var toDelete = new List<int>();
        if (maxId > 0)
        {
            var stored = await _channelMessagesRepository.GetMessagesAsync(id, 0, maxId);
            foreach (var saved in stored)
            {
                using var s = saved;
                toDelete.Add(MessageIds.GetId(s.AsSavedMessage().Get_OriginalMessage()));
            }
        }

        foreach (int messageId in toDelete)
        {
            await _channelMessagesRepository.DeleteMessageAsync(id, messageId);
        }

        if (toDelete.Count == 0)
        {
            return await BuildEmptyChannelUpdates(authKeyId, currentUserId);
        }

        int pts = await channelBox.IncrementPts(toDelete.Count);
        await _fanout.PushDeleteChannelMessagesAsync(id, currentUserId, toDelete, pts,
            toDelete.Count);
        await _unitOfWork.SaveAsync();

        int date = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
        var seqCtx = _updatesContextFactory.GetUpdatesContext(authKeyId, currentUserId);
        int seq = await seqCtx.IncrementSeq();

        var resultUpdates = new Vector();
        using (TLUpdate deleteUpdate = UpdateFanout.BuildDeleteChannelMessagesUpdate(
                   id, toDelete, pts, toDelete.Count))
        {
            resultUpdates.AppendTLObject(deleteUpdate.AsSpan());
        }
        var chatVector = new Vector();
        chatVector.AppendTLObject(channelBytes);
        var userVector = new Vector();
        AppendUser(currentUserId, ref userVector, currentUserId);

        _log.Debug($"📣 channels.DeleteHistory user:{currentUserId} channel:{id} maxId:{maxId} " +
                   $"deleted:{toDelete.Count} pts:{pts}");
        return Ferrite.TL.baseLayer.Updates.Builder()
            .UpdatesProperty(resultUpdates)
            .Users(userVector)
            .Chats(chatVector)
            .Date(date)
            .Seq(seq)
            .Build();
    }
}
