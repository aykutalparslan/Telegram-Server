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
using GroupParticipantsResult = Ferrite.TL.baseLayer.phone.TLGroupParticipants;

namespace Ferrite.Services.Phone.Handlers;

public sealed class GetGroupParticipantsHandler : GroupCallHandlerBase
{
    private readonly IGroupCallsRepository _groupCallsRepository;
    private readonly IUserRepository _userRepository;

    private const int DefaultLimit = 100;
    private const int MaxLimit = 200;

    private const int MaxSelectors = 200;

    public GetGroupParticipantsHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IAuthorizationRepository authorizationRepository, IGroupCallsRepository groupCallsRepository, IUserRepository userRepository, UpdateFanout fanout,
        GroupCallChatLink chatLink, IUpdatesContextFactory updatesContexts,
        IMTProtoTime time, GroupCallVideoOptions videoOptions,
        GroupCallMediaSourceMap sourceMap, ILogger log)
        : base(unitOfWork, chatParticipantsRepository, chatRepository, authorizationRepository, groupCallsRepository, fanout, chatLink, updatesContexts, time, videoOptions,
            sourceMap, log)
    {
        _groupCallsRepository = groupCallsRepository;
        _userRepository = userRepository;

    }

    [TLFunction(Constructors.baseLayer_GetGroupParticipants)]
    public async ValueTask<GroupParticipantsResult> Handle(long authKeyId, TLBytes q)
    {
        var request = (GetGroupParticipants)q;
        bool callRead = TryReadInputGroupCall(request.Get_CallView(), out long callId,
            out long accessHash);
        string offset = Encoding.UTF8.GetString(request.Offset);
        int limit = request.Limit;
        ReadIdSelectors(request.Ids, out List<long> userIds, out List<GroupCallReferencedPeer> peers);
        List<int> sources = ReadSourceSelectors(request.Sources);

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
        var view = call.AsGroupCallState();
        int count = view.ParticipantsCount;
        int version = view.Version;
        bool discarded = view.State == (int)GroupCallPersistenceState.Discarded;

        bool hasSelectors = userIds.Count > 0 || peers.Count > 0 || sources.Count > 0;
        (IReadOnlyList<TLDto.TLGroupCallParticipantState> rows, string? nextOffset) =
            discarded
                ? (Array.Empty<TLDto.TLGroupCallParticipantState>(), null)
                : hasSelectors
                    ? (await SelectAsync(callId, userIds, peers, sources), null)
                    : await PageAsync(callId, offset, limit);

        try
        {
            GroupCallViewer viewer = await BuildViewerAsync(callId, access.CurrentUserId,
                access.CanManageCall);
            Dictionary<long, GroupCallParticipantOverlay> overlays =
                await ReadOverlaysAsync(callId, access.CurrentUserId, rows);
            (List<byte[]> users, List<byte[]> chats) = await ReadRelatedRowsAsync(rows,
                access.CurrentUserId);

            Log.Debug($"📞 getGroupParticipants call:{callId} user:{access.CurrentUserId} " +
                      $"selectors:{hasSelectors} rows:{rows.Count} version:{version}");
            return BuildResult(rows, viewer, overlays, users, chats, count, version,
                nextOffset);
        }
        finally
        {
            foreach (TLDto.TLGroupCallParticipantState row in rows)
            {
                row.Dispose();
            }
        }
    }

    private static GroupParticipantsResult Error(string message) =>
        (GroupParticipantsResult)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));

    private async ValueTask<IReadOnlyList<TLDto.TLGroupCallParticipantState>> SelectAsync(
        long callId, IReadOnlyList<long> userIds, IReadOnlyList<GroupCallReferencedPeer> peers,
        IReadOnlyList<int> sources)
    {
        var rows = new List<TLDto.TLGroupCallParticipantState>();
        var seen = new HashSet<long>();

        foreach (long userId in userIds)
        {
            TLDto.TLGroupCallParticipantState? row = await _groupCallsRepository
                .GetParticipantAsync(callId, userId);
            Keep(row);
        }
        foreach (int source in sources)
        {
            TLDto.TLGroupCallParticipantState? row = await _groupCallsRepository
                .GetParticipantBySourceAsync(callId, source);
            Keep(row);
        }

        if (peers.Count > 0)
        {
            GroupCallParticipantPage page = await _groupCallsRepository
                .GetParticipantsPageAsync(callId, offset: null, MaxSelectors);
            foreach (TLDto.TLGroupCallParticipantState candidate in page.Participants)
            {
                var view = candidate.AsGroupCallParticipantState();
                int peerType = view.PeerType;
                long peerId = view.PeerId;
                long candidateUserId = view.UserId;
                bool wanted = false;
                foreach (GroupCallReferencedPeer selector in peers)
                {
                    if ((int)selector.Type == peerType && selector.Id == peerId)
                    {
                        wanted = true;
                        break;
                    }
                }
                if (wanted && !seen.Contains(candidateUserId))
                {
                    Keep(candidate);
                }
                else
                {
                    candidate.Dispose();
                }
            }
        }

        return rows;

        void Keep(TLDto.TLGroupCallParticipantState? row)
        {
            if (row == null)
            {
                return;
            }

            var view = row.Value.AsGroupCallParticipantState();
            if (view.Left || rows.Count >= MaxSelectors || !seen.Add(view.UserId))
            {
                row.Value.Dispose();
                return;
            }

            rows.Add(row.Value);
        }
    }

    private async ValueTask<(IReadOnlyList<TLDto.TLGroupCallParticipantState> Rows,
        string? NextOffset)> PageAsync(long callId, string offset, int limit)
    {
        GroupCallParticipantPage page = await _groupCallsRepository
            .GetParticipantsPageAsync(callId,
                string.IsNullOrEmpty(offset) ? null : offset, ResolveLimit(limit));
        return (page.Participants, page.NextOffset);
    }

    private static int ResolveLimit(int limit) => limit switch
    {
        <= 0 => DefaultLimit,
        > MaxLimit => MaxLimit,
        _ => limit,
    };

    private async ValueTask<Dictionary<long, GroupCallParticipantOverlay>>
        ReadOverlaysAsync(long callId, long viewerUserId,
            IReadOnlyList<TLDto.TLGroupCallParticipantState> rows)
    {
        var overlays = new Dictionary<long, GroupCallParticipantOverlay>();
        if (rows.Count == 0)
        {
            return overlays;
        }

        var mutes = new Dictionary<long, (bool MutedByYou, int? Volume)>();
        foreach (TLDto.TLGroupCallViewerParticipantState state in await _groupCallsRepository.GetViewerParticipantStatesAsync(callId,
                         viewerUserId))
        {
            using (state)
            {
                var view = state.AsGroupCallViewerParticipantState();
                mutes[view.TargetUserId] = (view.MutedByYou,
                    view.Flags[1] ? view.Volume : null);
            }
        }

        string? viewerMediaId = await GetMediaIdAsync(callId, viewerUserId);
        foreach (TLDto.TLGroupCallParticipantState row in rows)
        {
            var view = row.AsGroupCallParticipantState();
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
        ReadRelatedRowsAsync(IReadOnlyList<TLDto.TLGroupCallParticipantState> rows,
            long viewerUserId)
    {
        var users = new List<byte[]>();
        var chatIds = new List<long>();
        foreach (GroupCallReferencedPeer peer in GroupCallBuilders.ReferencedPeers(rows))
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

        return (users, await Fanout.GetChatBytesForViewerAsync(viewerUserId, chatIds));
    }

    private static GroupParticipantsResult BuildResult(
        IReadOnlyList<TLDto.TLGroupCallParticipantState> rows, GroupCallViewer viewer,
        IReadOnlyDictionary<long, GroupCallParticipantOverlay> overlays,
        IReadOnlyList<byte[]> userRows, IReadOnlyList<byte[]> chatRows, int count,
        int version, string? nextOffset)
    {
        var participants = new Vector();
        foreach (TLDto.TLGroupCallParticipantState row in rows)
        {
            long userId = row.AsGroupCallParticipantState().UserId;
            GroupCallParticipantOverlay overlay = overlays.TryGetValue(userId,
                out GroupCallParticipantOverlay stored)
                ? stored
                : GroupCallParticipantOverlay.None;
            using TLGroupCallParticipant built = GroupCallBuilders.BuildParticipant(row,
                viewer, overlay);
            participants.AppendTLObject(built.AsSpan());
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

        return GroupParticipants.Builder()
            .Count(count)
            .Participants(participants)
            .NextOffset(Encoding.UTF8.GetBytes(nextOffset ?? string.Empty))
            .Chats(chats)
            .Users(users)
            .Version(version)
            .Build();
    }

    private static void ReadIdSelectors(Vector ids, out List<long> userIds,
        out List<GroupCallReferencedPeer> peers)
    {
        userIds = new List<long>();
        peers = new List<GroupCallReferencedPeer>();
        int count = Math.Min(ids.Count, MaxSelectors);
        for (int i = 0; i < count; i++)
        {
            var peer = (InputPeerView)ids.ReadTLObject();
            if (peer.Is(out InputPeerUser user) && user.UserId > 0)
            {
                userIds.Add(user.UserId);
            }
            else if (peer.Is(out InputPeerChat chat) && chat.ChatId > 0)
            {
                peers.Add(new GroupCallReferencedPeer(TLPeer.PeerType.PeerChat,
                    chat.ChatId));
            }
            else if (peer.Is(out InputPeerChannel channel) && channel.ChannelId > 0)
            {
                peers.Add(new GroupCallReferencedPeer(TLPeer.PeerType.PeerChannel,
                    channel.ChannelId));
            }
        }
    }

    private static List<int> ReadSourceSelectors(VectorOfInt sources)
    {
        var result = new List<int>();
        int count = Math.Min(sources.Count, MaxSelectors);
        for (int i = 0; i < count; i++)
        {
            if (sources[i] != 0)
            {
                result.Add(sources[i]);
            }
        }

        return result;
    }
}
