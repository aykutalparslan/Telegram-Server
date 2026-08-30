// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Dialogs;

public readonly record struct DialogOrganizationState(int FolderId, bool Pinned,
    bool UnreadMark, int PinOrder)
{
    public static readonly DialogOrganizationState Default = new(0, false, false, 0);
}

public readonly record struct DialogFolderMove(DialogPeerKey Peer, int FolderId);

public sealed class DialogOrganizationStore
{
    private readonly IChatParticipantsRepository _chatParticipantsRepository;

    private readonly IChatRepository _chatRepository;
    private readonly IDialogOrganizationRepository _dialogOrganizationRepository;
    private readonly IUserRepository _userRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IUpdatesService _updates;
    private readonly IUpdatesContextFactory _updatesContextFactory;
    private readonly UpdateFanout _fanout;
    private readonly TimeProvider _time;

    public DialogOrganizationStore(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IDialogOrganizationRepository dialogOrganizationRepository, IUserRepository userRepository, IUpdatesService updates,
        IUpdatesContextFactory updatesContextFactory, UpdateFanout fanout,
        TimeProvider time)
    {
        _chatParticipantsRepository = chatParticipantsRepository;

        _chatRepository = chatRepository;
        _dialogOrganizationRepository = dialogOrganizationRepository;
        _userRepository = userRepository;

        _unitOfWork = unitOfWork;
        _updates = updates;
        _updatesContextFactory = updatesContextFactory;
        _fanout = fanout;
        _time = time;
    }

    public async Task<Dictionary<DialogPeerKey, DialogOrganizationState>>
        GetPeerStatesAsync(long userId) => await ReadPeerStatesAsync(
            _dialogOrganizationRepository, userId);

    internal static async Task<Dictionary<DialogPeerKey, DialogOrganizationState>>
        ReadPeerStatesAsync(IDialogOrganizationRepository repository, long userId)
    {
        IReadOnlyCollection<TLDialogPeerState> rows = await repository
            .GetPeerStatesAsync(userId);
        var result = new Dictionary<DialogPeerKey, DialogOrganizationState>();
        foreach (TLDialogPeerState row in rows)
        {
            using (row)
            {
                var value = row.AsDialogPeerState();
                var key = new DialogPeerKey((TLPeer.PeerType)value.PeerType,
                    value.PeerId);
                result[key] = new DialogOrganizationState(value.FolderId,
                    value.Pinned, value.UnreadMark, value.PinOrder);
            }
        }
        return result;
    }

    public async Task<TLBool> TogglePinAsync(long authKeyId, long userId,
        DialogPeerKey peer, bool pinned)
    {
        if (!await CanUsePeerAsync(userId, peer))
        {
            return BoolError(400, "PEER_ID_INVALID"u8);
        }

        Dictionary<DialogPeerKey, DialogOrganizationState> states =
            await GetPeerStatesAsync(userId);
        DialogOrganizationState current = states.GetValueOrDefault(peer,
            DialogOrganizationState.Default);
        int pinOrder = 0;
        if (pinned)
        {
            pinOrder = current.Pinned ? current.PinOrder :
                states.Values.Where(x => x.FolderId == current.FolderId && x.Pinned)
                    .Select(x => x.PinOrder).DefaultIfEmpty().Max() + 1;
        }

        if (!PutPeerState(userId, peer, current with
            {
                Pinned = pinned,
                PinOrder = pinOrder,
            }) || !await _unitOfWork.SaveAsync())
        {
            return BoolError(500, "INTERNAL_SERVER_ERROR"u8);
        }

        TLUpdate update = BuildDialogPinnedUpdate(peer, current.FolderId, pinned);
        await _updates.EnqueueUpdate(userId, update,
            UpdateDeliveryScope.ExcludingAuthKeys([authKeyId]));
        return BoolTrue.Builder().Build();
    }

    public async Task<TLBool> ReorderPinnedAsync(long authKeyId, long userId,
        int folderId, IReadOnlyList<DialogPeerKey> order)
    {
        if (folderId is not (0 or 1) || order.Distinct().Count() != order.Count)
        {
            return BoolError(400, "FOLDER_ID_INVALID"u8);
        }
        foreach (DialogPeerKey peer in order)
        {
            if (!await CanUsePeerAsync(userId, peer))
            {
                return BoolError(400, "PEER_ID_INVALID"u8);
            }
        }

        Dictionary<DialogPeerKey, DialogOrganizationState> states =
            await GetPeerStatesAsync(userId);
        if (order.Any(peer => states.GetValueOrDefault(peer,
                DialogOrganizationState.Default).FolderId != folderId))
        {
            return BoolError(400, "FOLDER_ID_INVALID"u8);
        }

        var requested = order.ToHashSet();
        foreach (var (peer, state) in states.Where(x =>
                     x.Value.FolderId == folderId && x.Value.Pinned))
        {
            if (!requested.Contains(peer) && !PutPeerState(userId, peer,
                    state with { Pinned = false, PinOrder = 0 }))
            {
                return BoolError(500, "INTERNAL_SERVER_ERROR"u8);
            }
        }
        for (int i = 0; i < order.Count; i++)
        {
            DialogPeerKey peer = order[i];
            DialogOrganizationState state = states.GetValueOrDefault(peer,
                DialogOrganizationState.Default);
            if (!PutPeerState(userId, peer, state with
                {
                    Pinned = true,
                    PinOrder = order.Count - i,
                }))
            {
                return BoolError(500, "INTERNAL_SERVER_ERROR"u8);
            }
        }
        if (!await _unitOfWork.SaveAsync())
        {
            return BoolError(500, "INTERNAL_SERVER_ERROR"u8);
        }

        TLUpdate update = BuildPinnedDialogsUpdate(folderId, order);
        await _updates.EnqueueUpdate(userId, update,
            UpdateDeliveryScope.ExcludingAuthKeys([authKeyId]));
        return BoolTrue.Builder().Build();
    }

    public async Task<TLBool> MarkUnreadAsync(long authKeyId, long userId,
        DialogPeerKey peer, bool unread)
    {
        if (!await CanUsePeerAsync(userId, peer))
        {
            return BoolError(400, "PEER_ID_INVALID"u8);
        }

        using TLDialogPeerState? stored = await _dialogOrganizationRepository.GetPeerStateAsync(userId, (int)peer.Type,
                peer.Id);
        DialogOrganizationState current = stored is null
            ? DialogOrganizationState.Default
            : ReadState(stored.Value);
        if (!PutPeerState(userId, peer, current with { UnreadMark = unread }) ||
            !await _unitOfWork.SaveAsync())
        {
            return BoolError(500, "INTERNAL_SERVER_ERROR"u8);
        }

        TLUpdate update = BuildDialogUnreadMarkUpdate(peer, unread);
        await _updates.EnqueueUpdate(userId, update,
            UpdateDeliveryScope.ExcludingAuthKeys([authKeyId]));
        return BoolTrue.Builder().Build();
    }

    public async Task<TLUpdates> EditPeerFoldersAsync(long authKeyId, long userId,
        IReadOnlyList<DialogFolderMove> moves)
    {
        if (moves.Select(x => x.Peer).Distinct().Count() != moves.Count ||
            moves.Any(x => x.FolderId is not (0 or 1)))
        {
            return UpdatesError(400, "FOLDER_ID_INVALID"u8);
        }
        foreach (DialogFolderMove move in moves)
        {
            if (!await CanUsePeerAsync(userId, move.Peer))
            {
                return UpdatesError(400, "PEER_ID_INVALID"u8);
            }
        }

        Dictionary<DialogPeerKey, DialogOrganizationState> states =
            await GetPeerStatesAsync(userId);
        var nextPinnedOrder = new Dictionary<int, int>
        {
            [0] = states.Values.Where(x => x.FolderId == 0 && x.Pinned)
                .Select(x => x.PinOrder).DefaultIfEmpty().Max(),
            [1] = states.Values.Where(x => x.FolderId == 1 && x.Pinned)
                .Select(x => x.PinOrder).DefaultIfEmpty().Max(),
        };
        foreach (DialogFolderMove move in moves)
        {
            DialogOrganizationState current = states.GetValueOrDefault(move.Peer,
                DialogOrganizationState.Default);
            int order = current.Pinned && current.FolderId != move.FolderId
                ? ++nextPinnedOrder[move.FolderId]
                : current.PinOrder;
            if (!PutPeerState(userId, move.Peer, current with
                {
                    FolderId = move.FolderId,
                    PinOrder = order,
                }))
            {
                return UpdatesError(500, "INTERNAL_SERVER_ERROR"u8);
            }
        }
        if (!await _unitOfWork.SaveAsync())
        {
            return UpdatesError(500, "INTERNAL_SERVER_ERROR"u8);
        }

        if (moves.Count == 0)
        {
            return _fanout.BuildUpdates(userId, [], [], [], Now(), seq: 0);
        }

        IUpdatesContext context = _updatesContextFactory.GetUpdatesContext(authKeyId,
            userId);
        int pts = await context.IncrementPts();
        byte[] updateBytes;
        using (TLUpdate update = BuildFolderPeersUpdate(moves, pts))
        {
            updateBytes = update.AsSpan().ToArray();
        }
        await _updates.EnqueueUpdate(userId,
            new TLUpdate(updateBytes, 0, updateBytes.Length),
            UpdateDeliveryScope.ExcludingAuthKeys([authKeyId]));

        var userIds = moves.Where(x => x.Peer.Type == TLPeer.PeerType.PeerUser)
            .Select(x => x.Peer.Id).Distinct().ToArray();
        var chatIds = moves.Where(x => x.Peer.Type is TLPeer.PeerType.PeerChat or
                TLPeer.PeerType.PeerChannel)
            .Select(x => x.Peer.Id).Distinct().ToArray();
        List<byte[]> chats = await _fanout.GetChatBytesForViewerAsync(userId, chatIds);
        int seq = await context.IncrementSeq();
        return _fanout.BuildUpdates(userId, [updateBytes], userIds, chats, Now(), seq);
    }

    public async ValueTask<bool> CanUsePeerAsync(long userId, DialogPeerKey peer)
    {
        if (peer.Id <= 0) return false;
        if (peer.Type == TLPeer.PeerType.PeerUser)
        {
            using TLUser? user = _userRepository.GetUser(peer.Id);
            return user != null;
        }
        if (peer.Type is not (TLPeer.PeerType.PeerChat or
            TLPeer.PeerType.PeerChannel)) return false;

        using TLChat? chat = await _chatRepository.GetChatAsync(peer.Id);
        if (chat == null) return false;
        using TLChatParticipantInfo? participant = await _chatParticipantsRepository.GetParticipantAsync(peer.Id, userId);
        if (participant == null) return false;
        int role = participant.Value.AsChatParticipantInfo().Role;
        return role is not ((int)ChatParticipantRole.Banned) and
            not ((int)ChatParticipantRole.Left);
    }

    private bool PutPeerState(long userId, DialogPeerKey peer,
        DialogOrganizationState state)
    {
        var builder = DialogPeerState.Builder().UserId(userId)
            .PeerType((int)peer.Type).PeerId(peer.Id).FolderId(state.FolderId)
            .PinOrder(state.PinOrder).Date(Now());
        if (state.Pinned) builder = builder.Pinned(true);
        if (state.UnreadMark) builder = builder.UnreadMark(true);
        using TLDialogPeerState row = builder.Build();
        return _dialogOrganizationRepository.PutPeerState(row);
    }

    private static DialogOrganizationState ReadState(TLDialogPeerState row)
    {
        var value = row.AsDialogPeerState();
        return new DialogOrganizationState(value.FolderId, value.Pinned,
            value.UnreadMark, value.PinOrder);
    }

    private static TLUpdate BuildDialogPinnedUpdate(DialogPeerKey peer,
        int folderId, bool pinned)
    {
        using TLDialogPeer dialogPeer = BuildDialogPeer(peer);
        var builder = UpdateDialogPinned.Builder().Peer(dialogPeer.AsSpan());
        if (pinned) builder = builder.Pinned(true);
        if (folderId != 0) builder = builder.FolderId(folderId);
        return builder.Build();
    }

    private static TLUpdate BuildPinnedDialogsUpdate(int folderId,
        IReadOnlyList<DialogPeerKey> order)
    {
        var peers = new Vector();
        foreach (DialogPeerKey peer in order)
        {
            using TLDialogPeer dialogPeer = BuildDialogPeer(peer);
            peers.AppendTLObject(dialogPeer.AsSpan());
        }
        var builder = UpdatePinnedDialogs.Builder().Order(peers);
        if (folderId != 0) builder = builder.FolderId(folderId);
        return builder.Build();
    }

    private static TLUpdate BuildDialogUnreadMarkUpdate(DialogPeerKey peer,
        bool unread)
    {
        using TLDialogPeer dialogPeer = BuildDialogPeer(peer);
        var builder = UpdateDialogUnreadMark.Builder().Peer(dialogPeer.AsSpan());
        if (unread) builder = builder.Unread(true);
        return builder.Build();
    }

    private static TLUpdate BuildFolderPeersUpdate(
        IReadOnlyList<DialogFolderMove> moves, int pts)
    {
        var peers = new Vector();
        foreach (DialogFolderMove move in moves)
        {
            using TLPeer peer = PeerResolver.BuildPeer(move.Peer.Type, move.Peer.Id);
            using FolderPeer folderPeer = FolderPeer.Builder().Peer(peer.AsSpan())
                .FolderId(move.FolderId).Build();
            peers.AppendTLObject(folderPeer.ToReadOnlySpan());
        }
        return UpdateFolderPeers.Builder().FolderPeers(peers).Pts(pts).PtsCount(1)
            .Build();
    }

    public static TLDialogPeer BuildDialogPeer(DialogPeerKey peer)
    {
        using TLPeer value = PeerResolver.BuildPeer(peer.Type, peer.Id);
        return DialogPeer.Builder().Peer(value.AsSpan()).Build();
    }

    private int Now() => (int)_time.GetUtcNow().ToUnixTimeSeconds();

    private static TLBool BoolError(int code, ReadOnlySpan<byte> message) =>
        (TLBool)RpcErrorGenerator.GenerateError(code, message);

    private static TLUpdates UpdatesError(int code, ReadOnlySpan<byte> message) =>
        (TLUpdates)RpcErrorGenerator.GenerateError(code, message);
}
