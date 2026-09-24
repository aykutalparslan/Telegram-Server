// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Services.Transport;

namespace Ferrite.Core.Connection;

public interface ISessionOutbox
{
    void Track(long authKeyId, long sessionId, object owner, MTProtoMessage original,
        MTProtoMessage sent);
    void MarkSent(long authKeyId, long sessionId, MTProtoMessage sent, long messageId);
    void Acknowledge(long authKeyId, long sessionId, long messageId);
    IReadOnlyList<MTProtoMessage> TakeFromOtherOwners(long authKeyId, long sessionId,
        object owner);
}
