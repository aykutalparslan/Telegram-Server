// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Chatlists;

internal static class ChatlistPeerCodec
{
    public static Vector BuildPeerVector(IReadOnlyList<DialogPeerKey> peers)
    {
        var result = new Vector();
        foreach (DialogPeerKey peer in peers)
        {
            using TLPeer value = PeerResolver.BuildPeer(peer.Type, peer.Id);
            result.AppendTLObject(value.AsSpan());
        }
        return result;
    }

    public static bool TryReadPeers(Vector source, out DialogPeerKey[] peers)
    {
        var result = new List<DialogPeerKey>(source.Count);
        int count = source.Count;
        for (int i = 0; i < count; i++)
        {
            Span<byte> bytes = source.ReadTLObject();
            PeerView peer = bytes;
            if (!PeerResolver.TryReadPeer(peer, out var value))
            {
                peers = [];
                return false;
            }
            result.Add(new DialogPeerKey(value.Type, value.Id));
        }
        peers = result.ToArray();
        return true;
    }
}
