// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.Services.Scheduling;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class GetScheduledMessagesHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IUserRepository _userRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ScheduledMessageStore _scheduled;
    private readonly DialogBuilder _dialogs;

    public GetScheduledMessagesHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IUserRepository userRepository,
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

    [TLFunction(Constructors.baseLayer_GetScheduledMessages)]
    public async Task<TLMessages> Handle(long authKeyId, TLBytes q)
    {
        long? principal = await ScheduledQueueAccess.AuthenticateAsync(_authorizationRepository, authKeyId);
        if (principal is not { } userId)
        {
            return Error(new ErrorMessage(400, "AUTH_KEY_INVALID"));
        }

        var request = (GetScheduledMessages)q;
        int[] requestedIds = request.Id.ToArray();
        DialogPeerKey? peer = PeerResolver.ResolveOptionalDialogPeer(request.Get_PeerView(),
            userId);
        ScheduledQueueAccess.Resolved resolved = await ScheduledQueueAccess.ValidateAsync(_userRepository, _chatRepository, _chatParticipantsRepository, userId, peer);
        if (resolved.Error is { } error)
        {
            return Error(error);
        }
        if (requestedIds.Length == 0)
        {
            return Error(new ErrorMessage(400, "MESSAGE_IDS_EMPTY"));
        }

        IReadOnlyList<ScheduledMessageStore.ScheduledSnapshot> queue =
            await _scheduled.GetQueueAsync(resolved.UserId, resolved.PeerType,
                resolved.PeerId);
        var byId = queue.ToDictionary(x => x.ScheduledId);
        var selected = new List<byte[]>(requestedIds.Length);
        var seen = new HashSet<int>();
        foreach (int id in requestedIds)
        {
            if (seen.Add(id) && byId.TryGetValue(id, out var entry))
            {
                selected.Add(entry.MessageBytes);
            }
        }

        return await _dialogs.BuildSelectedMessagesAsync(resolved.UserId,
            resolved.PeerType, resolved.PeerId, selected);
    }

    private static TLMessages Error(ErrorMessage error) =>
        (TLMessages)RpcErrorGenerator.GenerateError(error.Code,
            Encoding.UTF8.GetBytes(error.Message));
}
