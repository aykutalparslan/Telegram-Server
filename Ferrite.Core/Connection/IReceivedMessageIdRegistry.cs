// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Core.Connection;

public interface IReceivedMessageIdRegistry
{
    bool Contains(long authKeyId, long sessionId, long messageId);

    void Add(long authKeyId, long sessionId, long messageId);
}
