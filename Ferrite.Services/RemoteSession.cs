// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services;

public class RemoteSession
{
    public long SessionId { get; set; }
    public Guid NodeId { get; set; }

    public TLRemoteSession ToTl()
    {
        Ferrite.TL.baseLayer.dto.RemoteSession row =
            Ferrite.TL.baseLayer.dto.RemoteSession.Builder()
                .SessionId(SessionId)
                .NodeId(NodeId.ToByteArray())
                .Build();
        return row;
    }

    public static RemoteSession FromTl(TLRemoteSession row)
    {
        Ferrite.TL.baseLayer.dto.RemoteSession view = row.AsRemoteSession();
        if (view.NodeId.Length != 16)
        {
            throw new InvalidDataException("Remote session node id must be 16 bytes.");
        }
        return new RemoteSession
        {
            SessionId = view.SessionId,
            NodeId = new Guid(view.NodeId),
        };
    }
}
