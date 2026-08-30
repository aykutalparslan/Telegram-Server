// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Data.MessageBoxes;

public interface IMessageBox
{
    public ValueTask<int> Pts();
    public ValueTask<int> IncrementPtsForMessage(int peerType, long peerId, int messageId);
    public ValueTask<int> NextMessageId();
    public ValueTask<int> ReadMessages(int peerType, long peerId, int maxId);
    public ValueTask<int> ReadMessagesMaxId(int peerType, long peerId);
    public ValueTask<int> UnreadMessages();
    public ValueTask<int> UnreadMessages(int peerType, long peerId);
    public ValueTask<int> IncrementPts();
    public ValueTask<int> IncrementPts(int count);
}