// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.Services.Scheduling;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class SendMediaHandler
{
    private readonly IDocumentsRepository _documentsRepository;
    private readonly IPhotoRepository _photoRepository;

    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IUploadService _upload;
    private readonly IPhotoProcessingService _photos;
    private readonly MediaMessageSender _sender;
    private readonly PollStore _polls;
    private readonly MessageLocator _locator;
    private readonly ScheduledMessageSender _schedule;
    private readonly DraftStore _drafts;

    public SendMediaHandler(IUnitOfWork unitOfWork, IDocumentsRepository documentsRepository, IPhotoRepository photoRepository, IAuthorizationRepository authorizationRepository, IUploadService upload,
        IPhotoProcessingService photos, MediaMessageSender sender, PollStore polls,
        MessageLocator locator, ScheduledMessageSender schedule, DraftStore drafts)
    {
        _documentsRepository = documentsRepository;
        _photoRepository = photoRepository;

        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
        _upload = upload;
        _photos = photos;
        _sender = sender;
        _polls = polls;
        _locator = locator;
        _schedule = schedule;
        _drafts = drafts;
    }

    [TLFunction(Constructors.baseLayer_SendMedia)]
    public async Task<TLUpdates> Handle(long authKeyId, TLBytes q)
    {
        var auth = await _authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        if (auth == null)
        {
            return Error(ErrorMessages.InvalidAuthKey);
        }

        long userId = auth.Value.AsAuthInfo().UserId;
        SendMediaSnapshot snapshot;
        {
            var request = (SendMedia)q;
            using TLPeer peer = PeerResolver.PeerFromInputPeer(request.Get_PeerView(), userId);
            long peerId = GetPeerId(peer);
            if (peerId <= 0)
            {
                return Error(ErrorMessages.PeerIdInvalid);
            }

            snapshot = Snapshot(request, peer.Type, peerId);
        }

        MediaResolver.MediaCategory category = MediaResolver.GetCategory(
            snapshot.InputMediaBytes);
        MediaResolver.MediaCategory[] categories = category ==
            MediaResolver.MediaCategory.Invalid
            ? Array.Empty<MediaResolver.MediaCategory>()
            : new[] { category };
        PreparedMessageTarget target = await _sender.PrepareAsync(userId,
            snapshot.PeerType, snapshot.PeerId, snapshot.SendMessageBytes, categories);
        if (target.Error != null)
        {
            return Error(new ErrorMessage(400, target.Error));
        }

        bool queued = snapshot.ScheduleDate > 0 &&
                      _schedule.IsQueued(snapshot.ScheduleDate);
        byte[] mediaBytes;
        PollStore.PollSnapshot? poll = null;
        if (category == MediaResolver.MediaCategory.Poll)
        {
            if (queued)
            {
                return Error(new ErrorMessage(403, "METHOD_DISABLED"));
            }

            PollStore.PollInput input;
            if (!PollStore.TryReadInputPoll(snapshot.InputMediaBytes, out input))
            {
                return Error(ErrorMessages.MediaInvalid);
            }
            if (PollStore.ValidateDefinition(input) is { } invalid)
            {
                return Error(invalid);
            }
            PollStore.PollSnapshot created = await _polls.CreateAsync(input,
                _polls.UnixNow());
            poll = created;
            mediaBytes = PollStore.BuildMedia(created,
                Array.Empty<PollStore.VoteSnapshot>(), userId, _polls.UnixNow());
        }
        else
        {
            MediaResolver.MediaResolution resolution = await MediaResolver.ResolveAsync(
                snapshot.InputMediaBytes, _upload, _photos, _unitOfWork, _photoRepository, _documentsRepository);
            if (resolution.Error is { } error)
            {
                return Error(error);
            }
            if (resolution.MediaBytes == null)
            {
                return Error(ErrorMessages.InternalServerError);
            }
            mediaBytes = resolution.MediaBytes;
        }

        if (queued)
        {
            return await _schedule.ScheduleAsync(authKeyId, userId, target,
                new[] { new ScheduledMessageSender.ScheduledItem(
                    snapshot.SendMessageBytes, mediaBytes) },
                groupedId: 0, snapshot.ScheduleDate);
        }

        MediaSentBatch sent = await _sender.SendAsync(authKeyId, userId, target,
            snapshot.SendMessageBytes, mediaBytes);
        if (poll != null && !await PersistPollAsync(poll.Value, userId, sent))
        {
            return Error(ErrorMessages.InternalServerError);
        }
        await _drafts.ClearAfterSendAsync(authKeyId, userId, target.PeerType,
            target.PeerId, snapshot.SendMessageBytes);
        if (sent.PeerType == TLPeer.PeerType.PeerChannel)
        {
            return await _sender.BuildChannelResultAsync(authKeyId, sent);
        }
        return await _sender.BuildAlbumResultAsync(authKeyId, userId, target,
            new[] { sent });
    }

    private async Task<bool> PersistPollAsync(PollStore.PollSnapshot poll,
        long userId, MediaSentBatch sent)
    {
        MessageIdentity? identity = await _locator.ResolveIdentityAsync(userId,
            sent.PeerType, sent.PeerId, sent.Id);
        if (identity == null)
        {
            return false;
        }
        return _polls.Persist(poll, identity.Value) && await _unitOfWork.SaveAsync();
    }

    private static SendMediaSnapshot Snapshot(SendMedia request,
        TLPeer.PeerType peerType, long peerId)
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
            .Message(request.Message)
            .RandomId(request.RandomId);

        Flags flags = request.Flags;
        if (flags[0]) builder = builder.ReplyTo(request.ReplyTo);
        if (flags[2]) builder = builder.ReplyMarkup(request.ReplyMarkup);

        if (flags[3])
        {
            builder = builder.Entities(request.Entities);
        }
        if (flags[10]) builder = builder.ScheduleDate(request.ScheduleDate);
        if (flags[13]) builder = builder.SendAs(request.SendAs);
        if (flags[17]) builder = builder.QuickReplyShortcut(request.QuickReplyShortcut);
        if (flags[18]) builder = builder.Effect(request.Effect);
        if (flags[21]) builder = builder.AllowPaidStars(request.AllowPaidStars);
        if (flags[22]) builder = builder.SuggestedPost(request.SuggestedPost);

        using SendMessage sendMessage = builder.Build();
        return new SendMediaSnapshot(peerType, peerId, request.Media.ToArray(),
            sendMessage.ToReadOnlySpan().ToArray(),
            flags[10] ? request.ScheduleDate : 0);
    }

    private static long GetPeerId(TLPeer peer) => peer.Type switch
    {
        TLPeer.PeerType.PeerUser => peer.AsPeerUser().UserId,
        TLPeer.PeerType.PeerChat => peer.AsPeerChat().ChatId,
        TLPeer.PeerType.PeerChannel => peer.AsPeerChannel().ChannelId,
        _ => 0
    };

    private static TLUpdates Error(ErrorMessage error) =>
        (TLUpdates)RpcErrorGenerator.GenerateError(error.Code,
            Encoding.UTF8.GetBytes(error.Message));

    private readonly record struct SendMediaSnapshot(TLPeer.PeerType PeerType,
        long PeerId, byte[] InputMediaBytes, byte[] SendMessageBytes,
        int ScheduleDate);
}
