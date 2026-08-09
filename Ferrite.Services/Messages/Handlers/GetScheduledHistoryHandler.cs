// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

/// <summary>
/// The whole scheduled queue of one dialog. This is the read side of the durable
/// queue introduced; before it existed the handler answered with an empty
/// list, which told a client its scheduled messages had vanished.
/// </summary>
public sealed class GetScheduledHistoryHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IUserRepository _userRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ScheduledMessageStore _scheduled;
    private readonly DialogBuilder _dialogs;

    public GetScheduledHistoryHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IUserRepository userRepository,
        ScheduledMessageStore scheduled, DialogBuilder dialogs)
    {
        _authorizationRepository = authorizationRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;
        _userRepository = userRepository;

        _unitOfWork = unitOfWork;
        _scheduled = scheduled;
        _dialogs = dialogs;
    }

    [TLFunction(Constructors.baseLayer_GetScheduledHistory)]
    public async Task<TLMessages> Handle(long authKeyId, TLBytes q)
    {
        long? principal = await ScheduledQueueAccess.AuthenticateAsync(_authorizationRepository, authKeyId);
        if (principal is not { } userId)
        {
            return Error(new ErrorMessage(400, "AUTH_KEY_INVALID"));
        }

        var request = (GetScheduledHistory)q;
        long requestedHash = request.Hash;
        DialogPeerKey? peer = PeerResolver.ResolveOptionalDialogPeer(request.Get_PeerView(),
            userId);
        ScheduledQueueAccess.Resolved resolved = await ScheduledQueueAccess.ValidateAsync(_userRepository, _chatRepository, _chatParticipantsRepository, userId, peer);
        if (resolved.Error is { } error)
        {
            return Error(error);
        }

        IReadOnlyList<ScheduledMessageStore.ScheduledSnapshot> queue =
            await _scheduled.GetQueueAsync(resolved.UserId, resolved.PeerType,
                resolved.PeerId);
        if (queue.Count > 0 && ComputeHash(queue) == requestedHash)
        {
            return MessagesNotModified.Builder().Count(queue.Count).Build();
        }

        var selected = queue.Select(x => x.MessageBytes).ToList();
        return await _dialogs.BuildSelectedMessagesAsync(resolved.UserId,
            resolved.PeerType, resolved.PeerId, selected);
    }

    // The hash pinned TDLib sends for a scheduled queue: `get_vector_hash` over the
    // flat (scheduled id, edit date, date) triple of every entry
    // (`MessagesManager.cpp:19262-19275`). The client folds them in scheduled
    // MessageId order, and a scheduled MessageId packs the send date ABOVE the
    // scheduled id (`MessageId.cpp:15-26`), so that order is by send date and only
    // then by scheduled id -- not by scheduled id alone.
    public static long ComputeHash(
        IReadOnlyList<ScheduledMessageStore.ScheduledSnapshot> queue)
    {
        var numbers = new List<long>(queue.Count * 3);
        foreach (var entry in queue.OrderBy(x => x.SendDate)
                     .ThenBy(x => x.ScheduledId))
        {
            numbers.Add(entry.ScheduledId);
            numbers.Add(ReadEditDate(entry.MessageBytes));
            numbers.Add(entry.SendDate);
        }
        return TelegramListHash.Compute(numbers);
    }

    private static int ReadEditDate(byte[] messageBytes)
    {
        using var stored = new TLMessage(messageBytes, 0, messageBytes.Length);
        return stored.Type == TLMessage.MessageType.Message &&
               stored.AsMessage().Flags[15]
            ? stored.AsMessage().EditDate
            : 0;
    }

    private static TLMessages Error(ErrorMessage error) =>
        (TLMessages)RpcErrorGenerator.GenerateError(error.Code,
            Encoding.UTF8.GetBytes(error.Message));
}
