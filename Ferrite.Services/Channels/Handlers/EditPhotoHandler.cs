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

public sealed class EditPhotoHandler : ChannelsHandlerBase
{
    private readonly IPhotoRepository _photoRepository;

    public EditPhotoHandler(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IPhotoRepository photoRepository, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository, ICounterFactory counterFactory,
        IdAllocators ids, IUpdatesContextFactory updatesContextFactory,
        IUpdatesService updates, ISearchEngine search, IUploadService upload,
        IPhotoProcessingService photos, ILogger log, ChatRowStore chatRows,
        UpdateFanout fanout)
        : base(unitOfWork, channelMessagesRepository, authorizationRepository, channelAdminLogRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, counterFactory, ids, updatesContextFactory, updates,
            search, upload, photos, log, chatRows, fanout)
    {
        _photoRepository = photoRepository;

    }

    [TLFunction(Constructors.baseLayer_EditPhoto)]
    public async Task<Ferrite.TL.baseLayer.TLUpdates> Handle(long authKeyId, TLBytes q)
    {
        var request = (EditPhoto)q;
        long? channelId = ResolveInputChannelId(request.Get_ChannelView());
        byte[] photoBytes = request.Photo.ToArray();

        var (currentUserId, channelBytes, error) = await PrepareChannelMutation(authKeyId,
            channelId, creatorOnly: false, ChatAdminRightRequirement.ChangeInfo);
        if (error != null)
        {
            return error.Value;
        }

        var resolution = await ChatPhotos.ResolveAsync(photoBytes, _upload, _photos, _photoRepository);
        if (resolution.Error != null)
        {
            return ErrorUpdates(Encoding.UTF8.GetBytes(resolution.Error.Value.Message));
        }

        byte[] previousPhoto = ReadChannelPhoto(channelBytes);
        byte[] updatedChannelBytes;
        byte[] actionBytes;
        if (resolution.IsDelete)
        {
            updatedChannelBytes = _chatRows.UpdateStoredChannelPhotoEmpty(channelBytes);
            using TLMessageAction action = MessageActionChatDeletePhoto.Builder().Build();
            actionBytes = action.AsSpan().ToArray();
            _log.Debug($"📣 EditPhoto(empty) user:{currentUserId} channel:{channelId!.Value}");
        }
        else
        {
            updatedChannelBytes = _chatRows.UpdateStoredChannelPhoto(channelBytes, resolution.PhotoId);
            using TLMessageAction action = MessageActionChatEditPhoto.Builder()
                .Photo(resolution.PhotoBytes)
                .Build();
            actionBytes = action.AsSpan().ToArray();
            _log.Debug($"📣 EditPhoto user:{currentUserId} channel:{channelId!.Value} " +
                       $"photo:{resolution.PhotoId}");
        }

        byte[] logAction;
        using (TLChannelAdminLogEventAction logEventAction =
               ChannelAdminLogEventActionChangePhoto.Builder()
                   .PrevPhoto(previousPhoto)
                   .NewPhoto(resolution.IsDelete ? EmptyPhoto() : resolution.PhotoBytes!)
                   .Build())
        {
            logAction = logEventAction.AsSpan().ToArray();
        }
        await AppendAdminLogEventAsync(channelId.Value, currentUserId, logAction,
            (int)DateTimeOffset.Now.ToUnixTimeSeconds());

        return await EmitChannelServiceMessage(authKeyId, currentUserId, channelId.Value,
            updatedChannelBytes, actionBytes);
    }

    // The full `Photo` behind the compact row's `chatPhoto`, or `photoEmpty` when
    // the channel has none. The action's prev/new fields are not flag-gated.
    private byte[] ReadChannelPhoto(byte[] channelBytes)
    {
        using var stored = new TLChat(channelBytes, 0, channelBytes.Length);
        byte[]? photoBytes = _chatRows.GetStoredPhotoBytes(
            ChatPhotos.ReadPhotoId(stored.AsChannel().Get_PhotoView()));
        return photoBytes ?? EmptyPhoto();
    }

    private static byte[] EmptyPhoto()
    {
        using TLPhoto empty = PhotoEmpty.Builder().Id(0).Build();
        return empty.AsSpan().ToArray();
    }
}
