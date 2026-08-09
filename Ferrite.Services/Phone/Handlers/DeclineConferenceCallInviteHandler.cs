// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Services.Calls;
using Ferrite.Services.Calls.E2E;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.phone;
using Ferrite.Utils;
using TLDto = Ferrite.TL.baseLayer.dto;
using TLUpdatesResult = Ferrite.TL.baseLayer.TLUpdates;

namespace Ferrite.Services.Phone.Handlers;

/// <summary>
/// phone.declineConferenceCallInvite. Marks the invite service message the
/// caller was sent as missed and edits it on both sides, which is how the
/// inviter's client learns the invitation was answered. Declining an unknown or
/// already-answered message is a no-op: the client retries a decline whenever it
/// is unsure, so it must never fail.
/// </summary>
public sealed class DeclineConferenceCallInviteHandler : ConferenceCallHandlerBase
{
    private readonly IMessageRepository _messageRepository;

    public DeclineConferenceCallInviteHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IAuthorizationRepository authorizationRepository, IGroupCallsRepository groupCallsRepository, IMessageRepository messageRepository, UpdateFanout fanout,
        GroupCallChatLink chatLink, IUpdatesContextFactory updatesContexts,
        IMTProtoTime time, GroupCallVideoOptions videoOptions,
        GroupCallMediaSourceMap sourceMap, ILogger log, IGroupCallChainService chain)
        : base(unitOfWork, chatParticipantsRepository, chatRepository, authorizationRepository, groupCallsRepository, messageRepository, fanout, chatLink, updatesContexts, time, videoOptions,
            sourceMap, log, chain)
    {
        _messageRepository = messageRepository;

    }

    [TLFunction(Constructors.baseLayer_DeclineConferenceCallInvite)]
    public async ValueTask<TLUpdatesResult> Handle(long authKeyId, TLBytes q)
    {
        var request = (DeclineConferenceCallInvite)q;
        int msgId = request.MsgId;

        long userId = await ResolveUserIdAsync(authKeyId);
        if (userId == 0)
        {
            return Error(GroupCallErrors.AuthKeyInvalid);
        }

        InviteMessage? invite = await ReadInviteAsync(userId, msgId);
        if (invite == null || invite.Value.Missed)
        {
            // Unknown, not an invite, or already answered. An idempotent empty
            // result keeps a retry from surfacing as an error in the client.
            return await BuildConferenceResultAsync(authKeyId, userId,
                Array.Empty<byte[]>());
        }

        byte[] declinedAction = BuildDeclinedAction(invite.Value);
        int date = Now();

        byte[] selfUpdate = await EditInviteAsync(userId, authKeyId, msgId,
            declinedAction, date);
        await EditPeerCopyAsync(invite.Value.PeerUserId, userId, declinedAction, date,
            invite.Value.CallId);
        await UnitOfWork.SaveAsync();

        Log.Debug($"📞 declineConferenceCallInvite user:{userId} msg:{msgId} " +
                  $"call:{invite.Value.CallId} inviter:{invite.Value.PeerUserId}");
        return await BuildConferenceResultAsync(authKeyId, userId, new[] { selfUpdate },
            new[] { invite.Value.PeerUserId });
    }

    private readonly record struct InviteMessage(long CallId, bool Missed, bool Video,
        long PeerUserId, int Date);

    /// <summary>
    /// The stored invite as this account holds it. Anything that is not an
    /// unanswered conference invitation resolves to null, which the caller treats
    /// as "nothing to decline".
    /// </summary>
    private async ValueTask<InviteMessage?> ReadInviteAsync(long userId, int msgId)
    {
        using TLDto.TLSavedMessage? saved = await _messageRepository
            .GetMessageAsync(userId, msgId);
        if (saved == null)
        {
            return null;
        }

        TLMessage message = saved.Value.AsSavedMessage().Get_OriginalMessage();
        if (message.Type != TLMessage.MessageType.MessageService)
        {
            return null;
        }

        var service = message.AsMessageService();
        var action = new MessageActionView(service.Action);
        if (!action.Is(out MessageActionConferenceCall conference))
        {
            return null;
        }

        var peer = new PeerView(service.PeerId);
        if (!peer.Is(out PeerUser peerUser))
        {
            return null;
        }

        return new InviteMessage(conference.CallId, conference.Missed, conference.Video,
            peerUser.UserId, service.Date);
    }

    // Declining turns the open invitation into a missed call on both sides; the
    // call id and the video bit stay so the client can still render what it was.
    private static byte[] BuildDeclinedAction(InviteMessage invite)
    {
        var builder = MessageActionConferenceCall.Builder()
            .CallId(invite.CallId)
            .Missed(true);
        if (invite.Video)
        {
            builder = builder.Video(true);
        }

        using TLMessageAction action = builder.Build();
        return action.AsSpan().ToArray();
    }

    /// <summary>
    /// Rewrites one stored copy of the invite in place and returns the
    /// updateEditMessage that reports it. The message keeps its id and its
    /// original date; only the action and the pts move.
    /// </summary>
    private async ValueTask<byte[]?> RewriteAsync(long ownerId, long? authKeyId,
        int msgId, byte[] actionBytes)
    {
        using TLDto.TLSavedMessage? saved = await _messageRepository
            .GetMessageAsync(ownerId, msgId);
        if (saved == null)
        {
            return null;
        }

        TLMessage original = saved.Value.AsSavedMessage().Get_OriginalMessage();
        if (original.Type != TLMessage.MessageType.MessageService)
        {
            return null;
        }

        byte[] rewritten;
        {
            var service = original.AsMessageService();
            var builder = MessageService.Builder()
                .Id(service.Id)
                .OutProperty(service.OutProperty)
                .FromId(service.FromId)
                .PeerId(service.PeerId)
                .Date(service.Date)
                .Action(actionBytes);
            using TLMessage edited = builder.Build();
            rewritten = edited.AsSpan().ToArray();
        }

        int pts = await UpdatesContexts.GetUpdatesContext(authKeyId, ownerId)
            .IncrementPts();
        using (var edited = new TLMessage(rewritten, 0, rewritten.Length))
        {
            _messageRepository.PutMessage(ownerId, edited, pts);
        }

        using TLUpdate update = UpdateEditMessage.Builder()
            .Message(rewritten)
            .Pts(pts)
            .PtsCount(1)
            .Build();
        return update.AsSpan().ToArray();
    }

    private async ValueTask<byte[]> EditInviteAsync(long userId, long authKeyId,
        int msgId, byte[] actionBytes, int date) =>
        await RewriteAsync(userId, authKeyId, msgId, actionBytes) ??
        Array.Empty<byte>();

    /// <summary>
    /// The inviter's own copy carries a different message id, so it is found by
    /// walking that dialog for the invite naming the same call.
    /// </summary>
    private async Task EditPeerCopyAsync(long peerUserId, long declinerUserId,
        byte[] actionBytes, int date, long callId)
    {
        int? peerMsgId = await FindInviteIdAsync(peerUserId, declinerUserId, callId);
        if (peerMsgId == null)
        {
            return;
        }

        byte[]? update = await RewriteAsync(peerUserId, null, peerMsgId.Value,
            actionBytes);
        if (update != null)
        {
            await Fanout.EnqueueSerializedAsync(peerUserId, update);
        }
    }

    private async ValueTask<int?> FindInviteIdAsync(long ownerId, long dialogUserId,
        long callId)
    {
        IReadOnlyCollection<TLDto.TLSavedMessage> messages = await _messageRepository.GetMessagesAsync(ownerId);
        int? found = null;
        foreach (TLDto.TLSavedMessage saved in messages)
        {
            using (saved)
            {
                if (found != null)
                {
                    continue;
                }
                TLMessage message = saved.AsSavedMessage().Get_OriginalMessage();
                if (message.Type != TLMessage.MessageType.MessageService)
                {
                    continue;
                }
                var service = message.AsMessageService();
                var action = new MessageActionView(service.Action);
                if (!action.Is(out MessageActionConferenceCall conference) ||
                    conference.CallId != callId || conference.Missed)
                {
                    continue;
                }
                var peer = new PeerView(service.PeerId);
                if (peer.Is(out PeerUser peerUser) && peerUser.UserId == dialogUserId)
                {
                    found = service.Id;
                }
            }
        }

        return found;
    }
}
