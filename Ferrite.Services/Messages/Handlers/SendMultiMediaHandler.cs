// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.Services.Scheduling;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class SendMultiMediaHandler
{
    private readonly IDocumentsRepository _documentsRepository;
    private readonly IPhotoRepository _photoRepository;

    private readonly IAuthorizationRepository _authorizationRepository;

    private const int MaxAlbumSize = 10;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IUploadService _upload;
    private readonly IPhotoProcessingService _photos;
    private readonly MediaMessageSender _sender;
    private readonly IdAllocators _ids;
    private readonly ScheduledMessageSender _schedule;
    private readonly DraftStore _drafts;

    public SendMultiMediaHandler(IUnitOfWork unitOfWork, IDocumentsRepository documentsRepository, IPhotoRepository photoRepository, IAuthorizationRepository authorizationRepository, IUploadService upload,
        IPhotoProcessingService photos, MediaMessageSender sender,
        IdAllocators ids, ScheduledMessageSender schedule, DraftStore drafts)
    {
        _documentsRepository = documentsRepository;
        _photoRepository = photoRepository;

        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
        _upload = upload;
        _photos = photos;
        _sender = sender;
        _ids = ids;
        _schedule = schedule;
        _drafts = drafts;
    }

    [TLFunction(Constructors.baseLayer_SendMultiMedia)]
    public async Task<TLUpdates> Handle(long authKeyId, TLBytes q)
    {
        var auth = await _authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        if (auth == null)
        {
            return Error(ErrorMessages.InvalidAuthKey);
        }

        long userId = auth.Value.AsAuthInfo().UserId;
        AlbumSnapshot snapshot;
        {
            var request = (SendMultiMedia)q;
            Vector multiMedia = request.MultiMedia;
            if (multiMedia.Count > MaxAlbumSize)
            {
                return Error(ErrorMessages.MultiMediaTooLong);
            }
            if (multiMedia.Count == 0)
            {
                return Error(ErrorMessages.MediaEmpty);
            }

            using TLPeer peer = PeerResolver.PeerFromInputPeer(request.Get_PeerView(),
                userId);
            long peerId = GetPeerId(peer);
            if (peerId <= 0)
            {
                return Error(ErrorMessages.PeerIdInvalid);
            }

            var items = new List<AlbumItemSnapshot>(multiMedia.Count);
            var categories = new HashSet<MediaResolver.MediaCategory>();
            for (int i = 0; i < multiMedia.Count; i++)
            {
                var item = (InputSingleMedia)multiMedia.ReadTLObject();
                byte[] inputMediaBytes = item.Media.ToArray();
                MediaResolver.MediaCategory category = MediaResolver.GetCategory(
                    inputMediaBytes);
                if (category != MediaResolver.MediaCategory.Invalid)
                {
                    categories.Add(category);
                }
                items.Add(new AlbumItemSnapshot(inputMediaBytes,
                    BuildSendMessage(request, item, includeReplyTo: i == 0)));
            }
            snapshot = new AlbumSnapshot(peer.Type, peerId, items,
                categories.ToArray(),
                request.Flags[10] ? request.ScheduleDate : 0);
        }

        PreparedMessageTarget target = await _sender.PrepareAsync(userId,
            snapshot.PeerType, snapshot.PeerId, snapshot.Items[0].SendMessageBytes,
            snapshot.Categories);
        if (target.Error != null)
        {
            return Error(new ErrorMessage(400, target.Error));
        }

        var resolvedMedia = new List<byte[]>(snapshot.Items.Count);
        foreach (AlbumItemSnapshot item in snapshot.Items)
        {
            MediaResolver.MediaResolution resolution = await MediaResolver.ResolveAsync(
                item.InputMediaBytes, _upload, _photos, _unitOfWork, _photoRepository, _documentsRepository);
            if (resolution.Error is { } error)
            {
                return Error(error);
            }
            if (resolution.MediaBytes == null)
            {
                return Error(ErrorMessages.InternalServerError);
            }
            resolvedMedia.Add(resolution.MediaBytes);
        }

        long groupedId = await _ids.NextMediaGroupIdAsync();
        if (snapshot.ScheduleDate > 0 && _schedule.IsQueued(snapshot.ScheduleDate))
        {
            var queuedItems = new List<ScheduledMessageSender.ScheduledItem>(
                snapshot.Items.Count);
            for (int i = 0; i < snapshot.Items.Count; i++)
            {
                queuedItems.Add(new ScheduledMessageSender.ScheduledItem(
                    snapshot.Items[i].SendMessageBytes, resolvedMedia[i]));
            }
            return await _schedule.ScheduleAsync(authKeyId, userId, target,
                queuedItems, groupedId, snapshot.ScheduleDate);
        }

        var sentItems = new List<MediaSentBatch>(snapshot.Items.Count);
        for (int i = 0; i < snapshot.Items.Count; i++)
        {
            sentItems.Add(await _sender.SendAsync(authKeyId, userId, target,
                snapshot.Items[i].SendMessageBytes, resolvedMedia[i], groupedId));
        }
        await _drafts.ClearAfterSendAsync(authKeyId, userId, target.PeerType,
            target.PeerId, snapshot.Items[0].SendMessageBytes);
        return await _sender.BuildAlbumResultAsync(authKeyId, userId, target,
            sentItems);
    }

    private static byte[] BuildSendMessage(SendMultiMedia request,
        InputSingleMedia item, bool includeReplyTo)
    {
        var builder = SendMessage.Builder()
            .Silent(request.Silent)
            .Background(request.Background)
            .ClearDraft(request.ClearDraft)
            .Noforwards(request.Noforwards)
            .UpdateStickersetsOrder(request.UpdateStickersetsOrder)
            .InvertMedia(request.InvertMedia)
            .AllowPaidFloodskip(request.AllowPaidFloodskip)
            .Peer(request.Peer)
            .Message(item.Message)
            .RandomId(item.RandomId);

        Flags flags = request.Flags;
        if (includeReplyTo && flags[0]) builder = builder.ReplyTo(request.ReplyTo);
        if (flags[10]) builder = builder.ScheduleDate(request.ScheduleDate);
        if (flags[13]) builder = builder.SendAs(request.SendAs);
        if (flags[17]) builder = builder.QuickReplyShortcut(request.QuickReplyShortcut);
        if (flags[18]) builder = builder.Effect(request.Effect);
        if (flags[21]) builder = builder.AllowPaidStars(request.AllowPaidStars);
        if (item.Flags[0]) builder = builder.Entities(item.Entities);

        using SendMessage result = builder.Build();
        return result.ToReadOnlySpan().ToArray();
    }

    private static long GetPeerId(TLPeer peer) => peer.Type switch
    {
        TLPeer.PeerType.PeerUser => peer.AsPeerUser().UserId,
        TLPeer.PeerType.PeerChat => peer.AsPeerChat().ChatId,
        TLPeer.PeerType.PeerChannel => peer.AsPeerChannel().ChannelId,
        _ => 0,
    };

    private static TLUpdates Error(ErrorMessage error) =>
        (TLUpdates)RpcErrorGenerator.GenerateError(error.Code,
            Encoding.UTF8.GetBytes(error.Message));

    private readonly record struct AlbumItemSnapshot(byte[] InputMediaBytes,
        byte[] SendMessageBytes);

    private sealed record AlbumSnapshot(TLPeer.PeerType PeerType, long PeerId,
        IReadOnlyList<AlbumItemSnapshot> Items,
        IReadOnlyCollection<MediaResolver.MediaCategory> Categories,
        int ScheduleDate);
}
