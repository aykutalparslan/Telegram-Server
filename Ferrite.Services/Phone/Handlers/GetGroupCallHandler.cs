// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.Services.Calls;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.phone;
using Ferrite.Utils;
using TLDto = Ferrite.TL.baseLayer.dto;
using GroupCallResult = Ferrite.TL.baseLayer.phone.TLGroupCall;

namespace Ferrite.Services.Phone.Handlers;

public sealed class GetGroupCallHandler : GroupCallHandlerBase
{
    private readonly IGroupCallsRepository _groupCallsRepository;
    private readonly IUserRepository _userRepository;

    private const int DefaultParticipantLimit = 100;
    private const int MaxParticipantLimit = 200;

    public GetGroupCallHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IAuthorizationRepository authorizationRepository, IGroupCallsRepository groupCallsRepository, IUserRepository userRepository, UpdateFanout fanout,
        GroupCallChatLink chatLink, IUpdatesContextFactory updatesContexts,
        IMTProtoTime time, GroupCallVideoOptions videoOptions,
        GroupCallMediaSourceMap sourceMap, ILogger log)
        : base(unitOfWork, chatParticipantsRepository, chatRepository, authorizationRepository, groupCallsRepository, fanout, chatLink, updatesContexts, time, videoOptions, sourceMap, log)
    {
        _groupCallsRepository = groupCallsRepository;
        _userRepository = userRepository;

    }

    [TLFunction(Constructors.baseLayer_GetGroupCall)]
    public async ValueTask<GroupCallResult> Handle(long authKeyId, TLBytes q)
    {
        var request = (GetGroupCall)q;
        bool callRead = TryReadInputGroupCall(request.Get_CallView(), out long callId,
            out long accessHash);
        int limit = request.Limit;

        if (!callRead)
        {
            return Error(GroupCallErrors.GroupCallInvalid);
        }

        using GroupCallResolution resolution = await ResolveCallAsync(authKeyId, callId,
            accessHash, GroupCallAccessLevel.Read);
        if (resolution.Error != null)
        {
            return Error(resolution.Error);
        }

        GroupCallPeerAccess access = resolution.Access!;
        TLDto.TLGroupCallState call = resolution.Call!.Value;
        bool discarded = call.AsGroupCallState().State ==
                         (int)GroupCallPersistenceState.Discarded;

        GroupCallViewer viewer = await BuildViewerAsync(callId, access.CurrentUserId,
            access.CanManageCall);

        GroupCallParticipantPage page = discarded
            ? new GroupCallParticipantPage(
                Array.Empty<TLDto.TLGroupCallParticipantState>(), null)
            : await _groupCallsRepository.GetParticipantsPageAsync(callId,
                offset: null, ResolveLimit(limit));

        try
        {
            Dictionary<long, GroupCallParticipantOverlay> overlays =
                await ReadViewerOverlaysAsync(callId, access.CurrentUserId, discarded,
                    page.Participants);
            (List<byte[]> userRows, List<byte[]> chatRows) = await ReadRelatedRowsAsync(
                page.Participants, access.CurrentUserId);
            int videoCount = await CountUnmutedVideoAsync(callId, discarded);
            return BuildResult(call, viewer, page, overlays, userRows, chatRows,
                VideoOptions, videoCount);
        }
        finally
        {
            foreach (TLDto.TLGroupCallParticipantState participant in page.Participants)
            {
                participant.Dispose();
            }
        }
    }

    private static int ResolveLimit(int limit) => limit switch
    {
        <= 0 => DefaultParticipantLimit,
        > MaxParticipantLimit => MaxParticipantLimit,
        _ => limit,
    };

    private static GroupCallResult Error(string message) =>
        (GroupCallResult)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));

    private async ValueTask<Dictionary<long, GroupCallParticipantOverlay>>
        ReadViewerOverlaysAsync(long callId, long viewerUserId, bool discarded,
            IReadOnlyList<TLDto.TLGroupCallParticipantState> participants)
    {
        var overlays = new Dictionary<long, GroupCallParticipantOverlay>();
        if (discarded)
        {
            return overlays;
        }

        var mutes = new Dictionary<long, (bool MutedByYou, int? Volume)>();
        IReadOnlyList<TLDto.TLGroupCallViewerParticipantState> rows = await _groupCallsRepository.GetViewerParticipantStatesAsync(callId, viewerUserId);
        foreach (TLDto.TLGroupCallViewerParticipantState row in rows)
        {
            using (row)
            {
                var view = row.AsGroupCallViewerParticipantState();
                mutes[view.TargetUserId] = (view.MutedByYou,
                    view.Flags[1] ? view.Volume : null);
            }
        }

        string? viewerMediaId = await GetMediaIdAsync(callId, viewerUserId);
        foreach (TLDto.TLGroupCallParticipantState state in participants)
        {
            var view = state.AsGroupCallParticipantState();
            long targetUserId = view.UserId;
            string producerMediaId = Encoding.UTF8.GetString(view.MediaId);
            (bool mutedByYou, int? volume) = mutes.TryGetValue(targetUserId,
                out (bool MutedByYou, int? Volume) stored)
                ? stored
                : (false, null);
            overlays[targetUserId] = BuildOverlay(callId, viewerMediaId, producerMediaId,
                mutedByYou, volume);
        }

        return overlays;
    }

    private async ValueTask<(List<byte[]> Users, List<byte[]> Chats)>
        ReadRelatedRowsAsync(IReadOnlyList<TLDto.TLGroupCallParticipantState> participants,
            long viewerUserId)
    {
        var users = new List<byte[]>();
        var chatIds = new List<long>();
        foreach (GroupCallReferencedPeer peer in
                 GroupCallBuilders.ReferencedPeers(participants))
        {
            if (peer.Type == TLPeer.PeerType.PeerUser)
            {
                using TLUser? user = _userRepository.GetUser(peer.Id);
                if (user != null)
                {
                    using TLUser prepared = Fanout.WithStatus(viewerUserId, user.Value);
                    users.Add(prepared.AsSpan().ToArray());
                }
                continue;
            }

            chatIds.Add(peer.Id);
        }

        List<byte[]> chats = await Fanout.GetChatBytesForViewerAsync(viewerUserId,
            chatIds);
        return (users, chats);
    }

    private static GroupCallResult BuildResult(TLDto.TLGroupCallState call,
        GroupCallViewer viewer, GroupCallParticipantPage page,
        IReadOnlyDictionary<long, GroupCallParticipantOverlay> overlays,
        IReadOnlyList<byte[]> userRows, IReadOnlyList<byte[]> chatRows,
        GroupCallVideoOptions videoOptions, int unmutedVideoCount)
    {
        var participants = new Vector();
        foreach (TLDto.TLGroupCallParticipantState state in page.Participants)
        {
            long participantUserId = state.AsGroupCallParticipantState().UserId;
            GroupCallParticipantOverlay overlay =
                overlays.TryGetValue(participantUserId, out GroupCallParticipantOverlay stored)
                    ? stored
                    : GroupCallParticipantOverlay.None;
            using TLGroupCallParticipant row = GroupCallBuilders.BuildParticipant(state,
                viewer, overlay);
            participants.AppendTLObject(row.AsSpan());
        }

        var users = new Vector();
        foreach (byte[] user in userRows)
        {
            users.AppendTLObject(user);
        }
        var chats = new Vector();
        foreach (byte[] chat in chatRows)
        {
            chats.AppendTLObject(chat);
        }

        using TL.baseLayer.TLGroupCall groupCall =
            GroupCallBuilders.BuildCall(call, viewer, videoOptions, unmutedVideoCount);
        return PhoneGroupCall.Builder()
            .Call(groupCall.AsSpan())
            .Participants(participants)
            .ParticipantsNextOffset(
                Encoding.UTF8.GetBytes(page.NextOffset ?? string.Empty))
            .Chats(chats)
            .Users(users)
            .Build();
    }
}
