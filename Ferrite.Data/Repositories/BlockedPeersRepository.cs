// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public class BlockedPeersRepository : IBlockedPeersRepository
{
    private readonly IKVStore _store;
    public BlockedPeersRepository(IKVStore store)
    {
        _store = store;
        store.SetSchema(new TableDefinition("ferrite", "blocked_peers",
            new KeyDefinition("pk",
                new DataColumn { Name = "user_id", Type = DataType.Long },
                new DataColumn { Name = "peer_id", Type = DataType.Long },
                new DataColumn { Name = "peer_type", Type = DataType.Int })));
    }
    public bool PutBlockedPeer(long userId, long peerId, PeerType peerType, DateTimeOffset date)
    {
        var blockedBytes = BlockedPeer.Builder()
            .PeerType((int)peerType)
            .PeerId(peerId)
            .Date((int)date.ToUnixTimeSeconds())
            .Build().TLBytes!.Value;
        return _store.Put(blockedBytes.AsSpan().ToArray(), userId, peerId, (int)peerType);
    }

    public bool DeleteBlockedPeer(long userId, long peerId, PeerType peerType)
    {
        return _store.Delete(userId, peerId, (int)peerType);
    }

    public IReadOnlyList<TLBlockedPeer> GetBlockedPeers(long userId)
    {
        List<TLBlockedPeer> blockedPeers = new();
        var iter = _store.Iterate(userId);
        foreach (var peerBlockedBytes in iter)
        {
            blockedPeers.Add(new TLBlockedPeer(peerBlockedBytes, 0 , peerBlockedBytes.Length));
        }

        return blockedPeers;
    }
}
