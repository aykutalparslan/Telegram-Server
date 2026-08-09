// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public interface IMessageRepository
{
    public bool PutMessage(long userId, TLMessage message, int pts);
    public IReadOnlyCollection<TLSavedMessage> GetMessages(long userId, TLInputPeer? peerId = null);
    public ValueTask<IReadOnlyCollection<TLSavedMessage>> GetMessagesAsync(long userId, TLInputPeer? peerId = null);
    public IReadOnlyCollection<TLSavedMessage> GetMessages(long userId, int pts, int maxPts, DateTimeOffset date);
    public ValueTask<IReadOnlyCollection<TLSavedMessage>> GetMessagesAsync(long userId, int pts, int maxPts, DateTimeOffset date);
    public TLSavedMessage? GetMessage(long userId, int messageId);
    public ValueTask<TLSavedMessage?> GetMessageAsync(long userId, int messageId);
    public bool DeleteMessage(long userId, int id);
    public ValueTask<bool> DeleteMessageAsync(long userId, int id);
}