// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Core;

public class DataCenter : IDataCenter
{
    public DataCenter(int id, string ipAddress, int port, bool mediaOnly)
    {
        Id = id;
        IpAddress = ipAddress;
        Port = port;
        MediaOnly = mediaOnly;
    }

    public int Id { get; }
    public string IpAddress { get; }
    public int Port { get; }
    public bool MediaOnly { get; }
}
