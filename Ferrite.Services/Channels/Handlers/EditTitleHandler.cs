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

public sealed class EditTitleHandler : ChannelsHandlerBase
{
    public EditTitleHandler(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository, ICounterFactory counterFactory,
        IdAllocators ids, IUpdatesContextFactory updatesContextFactory,
        IUpdatesService updates, ISearchEngine search, IUploadService upload,
        IPhotoProcessingService photos, ILogger log, ChatRowStore chatRows,
        UpdateFanout fanout)
        : base(unitOfWork, channelMessagesRepository, authorizationRepository, channelAdminLogRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, counterFactory, ids, updatesContextFactory, updates,
            search, upload, photos, log, chatRows, fanout)
    {
    }

    [TLFunction(Constructors.baseLayer_EditTitle)]
    public async Task<Ferrite.TL.baseLayer.TLUpdates> Handle(long authKeyId, TLBytes q)
    {
        var request = (EditTitle)q;
        long? channelId = ResolveInputChannelId(request.Get_ChannelView());
        byte[] title = request.Title.ToArray();

        var (currentUserId, channelBytes, error) = await PrepareChannelMutation(authKeyId,
            channelId, creatorOnly: false, ChatAdminRightRequirement.ChangeInfo);
        if (error != null)
        {
            return error.Value;
        }

        byte[] previousTitle = ReadChannelTitle(channelBytes);
        byte[] updatedChannelBytes = _chatRows.UpdateStoredChannelTitle(channelBytes, title);

        byte[] logAction;
        using (TLChannelAdminLogEventAction logEventAction =
               ChannelAdminLogEventActionChangeTitle.Builder()
                   .PrevValue(previousTitle)
                   .NewValue(title)
                   .Build())
        {
            logAction = logEventAction.AsSpan().ToArray();
        }
        await AppendAdminLogEventAsync(channelId!.Value, currentUserId, logAction,
            (int)DateTimeOffset.Now.ToUnixTimeSeconds(),
            $"{Encoding.UTF8.GetString(previousTitle)} {Encoding.UTF8.GetString(title)}");

        byte[] actionBytes;
        using (TLMessageAction action = MessageActionChatEditTitle.Builder().Title(title).Build())
        {
            actionBytes = action.AsSpan().ToArray();
        }

        string username = ReadChannelUsername(updatedChannelBytes);
        if (username.Length > 0)
        {
            await _search.IndexChat(new ChatSearchModel(channelId!.Value, username,
                Encoding.UTF8.GetString(title)));
        }

        _log.Debug($"📣 EditTitle user:{currentUserId} channel:{channelId!.Value}");
        return await EmitChannelServiceMessage(authKeyId, currentUserId, channelId.Value,
            updatedChannelBytes, actionBytes);
    }

    private static byte[] ReadChannelTitle(byte[] channelBytes)
    {
        using var stored = new TLChat(channelBytes, 0, channelBytes.Length);
        return stored.AsChannel().Title.ToArray();
    }
}
