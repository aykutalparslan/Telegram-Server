// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Calls;

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

    public byte[] SetCallFlags(GroupCallPeerKind kind, byte[] chatBytes, bool callActive,
        bool callNotEmpty) => SetCallFlagsForRow(kind == GroupCallPeerKind.BasicGroup,
        chatBytes, callActive, callNotEmpty);

    private byte[] SetCallFlagsForRow(bool isBasicGroup, byte[] chatBytes,
        bool callActive, bool callNotEmpty) => isBasicGroup
        ? _chatRows.UpdateStoredChatCallState(chatBytes, callActive, callNotEmpty)
        : _chatRows.UpdateStoredChannelCallState(chatBytes, callActive, callNotEmpty);

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
