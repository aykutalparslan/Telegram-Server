// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Data;

public interface IMessageBox
{
    /// <summary>
    /// 
    /// </summary>
    /// <returns>Current event sequence number.</returns>
    public ValueTask<int> Pts();
    /// <summary>
    /// Increments the current event sequence number and
    /// adds an unread message to the message box for the peer.
    /// </summary>
    /// <param name="peerId">The message source.</param>
    /// <param name="messageId">the message Id.</param>
    /// <returns>Event sequence number after increment.</returns>
    public ValueTask<int> IncrementPtsForMessage(int peerType, long peerId, int messageId);
    /// <summary>
    /// Increments the MessageId counter.
    /// </summary>
    /// <returns>MessageId after the increment.</returns>
    public ValueTask<int> NextMessageId();
    /// <summary>
    /// Marks the messages with lower Id's than the <paramref name="maxId"/> as read.
    /// </summary>
    /// <param name="peerId">The message source.</param>
    /// <param name="maxId">The maximum Id for the messages to be read.</param>
    /// <returns>The number of unread messages remaining.</returns>
    public ValueTask<int> ReadMessages(int peerType, long peerId, int maxId);
    /// <summary>
    /// Retrieves the MaxId of the read messages for the <paramref name="peerId"/>.
    /// </summary>
    /// <param name="peerId">The message source.</param>
    /// <returns>MaxI.</returns>
    public ValueTask<int> ReadMessagesMaxId(int peerType, long peerId);
    /// <summary>
    /// Retrieves the total number of unread messages.
    /// </summary>
    /// <returns>Total number of unread messages.</returns>
    public ValueTask<int> UnreadMessages();
    /// <summary>
    /// Retrieves the number of unread messages from the <paramref name="peerId"/>.
    /// </summary>
    /// <param name="peerId">The message source.</param>
    /// <returns>Number of unread messages.</returns>
    public ValueTask<int> UnreadMessages(int peerType, long peerId);
    /// <summary>
    ///  Increments the current event sequence number.
    /// </summary>
    /// <returns>Event sequence number after increment.</returns>
    public ValueTask<int> IncrementPts();
    /// <summary>
    /// Increments the current event sequence number by <paramref name="count"/>.
    /// Used by multi-event updates (e.g. deleting several messages) so the new
    /// pts equals previousPts + pts_count, matching the client gap check
    /// local_pts + pts_count === pts.
    /// </summary>
    /// <param name="count">Number of events generated.</param>
    /// <returns>Event sequence number after the increment.</returns>
    public ValueTask<int> IncrementPts(int count);
}