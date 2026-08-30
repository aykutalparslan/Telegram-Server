// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Common;

public interface IDataCenter
{
    public int Id { get; }
    public string IpAddress { get; }
    public int Port { get; }
    public bool MediaOnly { get; }
}
