// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public interface IBlockedPeersRepository
{
    public bool PutBlockedPeer(long userId, long peerId, PeerType peerType, DateTimeOffset date);
    public bool DeleteBlockedPeer(long userId, long peerId, PeerType peerType);
    public IReadOnlyList<TLBlockedPeer> GetBlockedPeers(long userId);
}