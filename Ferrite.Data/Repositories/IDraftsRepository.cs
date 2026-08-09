// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public interface IDraftsRepository
{
    bool PutDraft(TLDraftInfo draft);
    ValueTask<TLDraftInfo?> GetDraftAsync(long userId, int peerType, long peerId,
        int topMsgId);
    ValueTask<IReadOnlyCollection<TLDraftInfo>> GetDraftsAsync(long userId);
    bool DeleteDraft(long userId, int peerType, long peerId, int topMsgId);
    bool DeleteDrafts(long userId);
}
