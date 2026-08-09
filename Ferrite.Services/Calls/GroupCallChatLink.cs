// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Calls;

// The call link a full-chat/full-channel row carries for one viewer: the active
// call itself plus that viewer's own default join-as peer. Both members own
// pooled memory, so the builder disposes them after the full row is built.
public sealed class GroupCallFullLink : IDisposable
{
    public static readonly GroupCallFullLink None = new(null, null);

    public GroupCallFullLink(TLInputGroupCall? call, TLPeer? defaultJoinAs)
    {
        Call = call;
        DefaultJoinAs = defaultJoinAs;
    }

    public TLInputGroupCall? Call { get; }

    public TLPeer? DefaultJoinAs { get; }

    public void Dispose()
    {
        Call?.Dispose();
        DefaultJoinAs?.Dispose();
    }
}

// Keeps the stored chat/channel rows agreeing with group-call state: the compact
// row's call_active/call_not_empty flags, and the per-viewer call/default-join-as
// fields of the full rows. Row mutation itself stays in ChatRowStore, which is
// where every other compact-row write lives.
public sealed class GroupCallChatLink
{
    private readonly IChatRepository _chatRepository;
    private readonly IGroupCallsRepository _groupCallsRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ChatRowStore _chatRows;

    public GroupCallChatLink(IUnitOfWork unitOfWork, IChatRepository chatRepository, IGroupCallsRepository groupCallsRepository, ChatRowStore chatRows)
    {
        _chatRepository = chatRepository;
        _groupCallsRepository = groupCallsRepository;

        _unitOfWork = unitOfWork;
        _chatRows = chatRows;
    }

    // Writes the compact-row call flags for the hosting peer and returns the
    // updated row bytes, so callers can put the fresh chat into their Updates
    // result without a second read. A basic-group row bumps its version; a
    // channel row has none.
    public byte[] SetCallFlags(GroupCallPeerKind kind, byte[] chatBytes, bool callActive,
        bool callNotEmpty) => SetCallFlagsForRow(kind == GroupCallPeerKind.BasicGroup,
        chatBytes, callActive, callNotEmpty);

    // Only the row shape matters here: a basic group stores a chat# row, and both
    // megagroups and broadcast channels store a channel# row.
    private byte[] SetCallFlagsForRow(bool isBasicGroup, byte[] chatBytes,
        bool callActive, bool callNotEmpty) => isBasicGroup
        ? _chatRows.UpdateStoredChatCallState(chatBytes, callActive, callNotEmpty)
        : _chatRows.UpdateStoredChannelCallState(chatBytes, callActive, callNotEmpty);

    // Reads the hosting peer's row, applies the call flags, and returns the
    // updated bytes. Used by the paths that do not already hold the row.
    public async ValueTask<byte[]?> SetCallFlagsAsync(GroupCallPeerRef peer,
        bool callActive, bool callNotEmpty)
    {
        using var stored = await _chatRepository.GetChatAsync(peer.Id);
        if (stored == null)
        {
            return null;
        }

        return SetCallFlagsForRow(peer.Type == GroupCallPeerType.Chat,
            stored.Value.AsSpan().ToArray(), callActive, callNotEmpty);
    }

    // The full-chat/full-channel link for one viewer. Resolved before the full-row
    // builder is created because the builders are ref structs that cannot span an
    // await, and because a viewer must never receive another account's join-as.
    public async ValueTask<GroupCallFullLink> ResolveFullLinkAsync(
        GroupCallPeerType peerType, long peerId, long viewerUserId,
        CancellationToken cancellationToken = default)
    {
        using TLDto.TLGroupCallState? call = await _groupCallsRepository
            .GetActiveCallByPeerAsync((int)peerType, peerId, cancellationToken);
        if (call == null ||
            call.Value.AsGroupCallState().State == (int)GroupCallPersistenceState.Discarded)
        {
            return GroupCallFullLink.None;
        }

        TLInputGroupCall inputCall = GroupCallBuilders.BuildInputGroupCall(call.Value);
        TLPeer? joinAs = null;
        try
        {
            using TLDto.TLGroupCallDefaultJoinAs? stored = await _groupCallsRepository
                .GetDefaultJoinAsAsync(viewerUserId, (int)peerType, peerId, cancellationToken);
            if (stored != null)
            {
                var row = stored.Value.AsGroupCallDefaultJoinAs();
                // The supported identity boundary is self-only. Do not let a
                // stale or externally-written channel/chat row widen that boundary
                // when a full chat is rendered.
                if (row.JoinAsPeerType == (int)TLPeer.PeerType.PeerUser &&
                    row.JoinAsPeerId == viewerUserId)
                {
                    joinAs = PeerResolver.BuildPeer(TLPeer.PeerType.PeerUser,
                        viewerUserId);
                }
            }
        }
        catch
        {
            inputCall.Dispose();
            throw;
        }

        return new GroupCallFullLink(inputCall, joinAs);
    }
}
