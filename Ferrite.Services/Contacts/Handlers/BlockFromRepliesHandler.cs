// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.contacts;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Handlers.ContactMethods;

public sealed class BlockFromRepliesHandler : ContactsHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IBlockedPeersRepository _blockedPeersRepository;
    private readonly IMessageRepository _messageRepository;
    private readonly IUserRepository _userRepository;

    private readonly MessageStore _messages;
    private readonly ModerationStore _moderation;
    private readonly UpdateFanout _fanout;
    private readonly TimeProvider _timeProvider;

    public BlockFromRepliesHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IBlockedPeersRepository blockedPeersRepository, IMessageRepository messageRepository, IContactsRepository contactsRepository, IUserRepository userRepository, IUserStatusRepository userStatusRepository, ISearchEngine search,
        IUpdatesService updates, IUpdatesContextFactory updatesContextFactory,
        MessageStore messages, ModerationStore moderation, UpdateFanout fanout,
        TimeProvider timeProvider)
        : base(unitOfWork, contactsRepository, userRepository, userStatusRepository, search, updates, updatesContextFactory)
    {
        _authorizationRepository = authorizationRepository;
        _blockedPeersRepository = blockedPeersRepository;
        _messageRepository = messageRepository;
        _userRepository = userRepository;

        _messages = messages;
        _moderation = moderation;
        _fanout = fanout;
        _timeProvider = timeProvider;
    }

    [TLFunction(Constructors.baseLayer_BlockFromReplies)]
    public async Task<TLUpdates> Handle(long authKeyId, TLBytes q)
    {
        long userId;
        using (TLAuthInfo? auth = await _authorizationRepository
                   .GetAuthorizationAsync(authKeyId))
        {
            if (auth == null || !auth.Value.AsAuthInfo().LoggedIn)
            {
                return Error(401, "AUTH_KEY_INVALID");
            }
            userId = auth.Value.AsAuthInfo().UserId;
        }

        var request = (BlockFromReplies)q;
        bool deleteMessage = request.DeleteMessage;
        bool deleteHistory = request.DeleteHistory;
        bool reportSpam = request.ReportSpam;
        int msgId = request.MsgId;

        if (msgId <= 0)
        {
            return Error(400, "MSG_ID_INVALID");
        }

        using TLSavedMessage? saved = await _messageRepository
            .GetMessageAsync(userId, msgId);
        if (saved == null)
        {
            return Error(400, "MSG_ID_INVALID");
        }

        TLMessage message = saved.Value.AsSavedMessage().Get_OriginalMessage();
        if (!TryReadAuthor(message, out long authorId) ||
            !MessageStore.TryReadStoredMessageInfo(message,
                out StoredMessageInfo info))
        {
            return Error(400, "MESSAGE_AUTHOR_REQUIRED");
        }
        TLPeer.PeerType conversationType = info.PeerType;
        long conversationId = info.PeerId;

        if (authorId == userId)
        {
            return Error(400, "MESSAGE_AUTHOR_REQUIRED");
        }
        using (TLUser? author = _userRepository.GetUser(authorId))
        {
            if (author == null)
            {
                return Error(400, "USER_ID_INVALID");
            }
        }

        _blockedPeersRepository.PutBlockedPeer(userId, authorId,
            PeerType.User, _timeProvider.GetUtcNow());

        List<int> deletedIds = deleteHistory
            ? await _messages.DeleteConversationAsync(userId, conversationType,
                conversationId, maxId: 0, minDate: null, maxDate: null)
            : [];
        if (!deleteHistory && deleteMessage)
        {
            _messages.DeleteMessages(userId, [msgId]);
            deletedIds.Add(msgId);
        }

        if (reportSpam)
        {
            long reportId = await _moderation.RecordReportAsync(userId,
                ModerationReportKind.PeerSpam, conversationType, conversationId,
                messageIds: [msgId], subjectUserId: authorId);
            if (reportId == 0)
            {
                return Error(500, "INTERNAL_SERVER_ERROR");
            }
            await _moderation.SetActionBarAsync(userId, conversationType,
                conversationId, hidden: true, reportedSpam: true);
        }

        if (!await _unitOfWork.SaveAsync())
        {
            return Error(500, "INTERNAL_SERVER_ERROR");
        }

        var blocked = new BlockedPeerKey(authorId, PeerType.User);
        await EnqueuePeerBlockedUpdate(userId, blocked, blocked: true,
            myStoriesFrom: false);

        IUpdatesContext userCtx = _updatesContextFactory.GetUpdatesContext(authKeyId,
            userId);
        int pts = await _fanout.AdvanceAndEnqueueDeleteMessagesAsync(userId,
            deletedIds, userCtx);
        return BuildResult(userId, authorId, deletedIds, pts);
    }

    private TLUpdates BuildResult(long userId, long authorId,
        IReadOnlyList<int> deletedIds, int pts)
    {
        var updateBytes = new List<byte[]>(2);
        using (TLPeer peer = new PeerUser(authorId))
        using (TLUpdate blockedUpdate = UpdatePeerBlocked.Builder()
                   .Blocked(true)
                   .PeerId(peer.AsSpan())
                   .Build())
        {
            updateBytes.Add(blockedUpdate.AsSpan().ToArray());
        }
        if (deletedIds.Count > 0)
        {
            var ids = new VectorOfInt();
            foreach (int id in deletedIds)
            {
                ids.Append(id);
            }
            using TLUpdate deleteUpdate = UpdateDeleteMessages.Builder()
                .Messages(ids)
                .Pts(pts)
                .PtsCount(deletedIds.Count)
                .Build();
            updateBytes.Add(deleteUpdate.AsSpan().ToArray());
        }

        return _fanout.BuildUpdates(userId, updateBytes, [userId, authorId], [],
            (int)_timeProvider.GetUtcNow().ToUnixTimeSeconds(), seq: 0);
    }

    private static bool TryReadAuthor(TLMessage message, out long authorId)
    {
        authorId = 0;
        if (message.Type != TLMessage.MessageType.Message)
        {
            return false;
        }

        var body = message.AsMessage();
        if (body.Flags[8])
        {
            return body.Get_FromIdView().Is(out PeerUser from) &&
                   TrySet(from.UserId, out authorId);
        }
        return !body.OutProperty &&
               body.Get_PeerIdView().Is(out PeerUser peer) &&
               TrySet(peer.UserId, out authorId);
    }

    private static bool TrySet(long value, out long target)
    {
        target = value;
        return value > 0;
    }

    private static TLUpdates Error(int code, string message) =>
        (TLUpdates)RpcErrorGenerator.GenerateError(code,
            Encoding.UTF8.GetBytes(message));
}
