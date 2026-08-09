// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public interface IChatParticipantsRepository
{
    public bool PutParticipant(TLChatParticipantInfo participant);
    public ValueTask<TLChatParticipantInfo?> GetParticipantAsync(long chatId, long userId);
    public ValueTask<IReadOnlyCollection<TLChatParticipantInfo>> GetParticipantsAsync(long chatId);
    public ValueTask<IReadOnlyCollection<TLChatParticipantInfo>> GetParticipantsByUserAsync(long userId);
    public bool DeleteParticipant(long chatId, long userId);
    public bool DeleteParticipants(long chatId);
}
