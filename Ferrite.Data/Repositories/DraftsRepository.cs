// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public sealed class DraftsRepository : IDraftsRepository
{
    private readonly IKVStore _drafts;

    public DraftsRepository(IKVStore drafts)
    {
        _drafts = drafts;
        drafts.SetSchema(new TableDefinition("ferrite", "drafts",
            new KeyDefinition("pk",
                new DataColumn { Name = "user_id", Type = DataType.Long },
                new DataColumn { Name = "peer_type", Type = DataType.Int },
                new DataColumn { Name = "peer_id", Type = DataType.Long },
                new DataColumn { Name = "top_msg_id", Type = DataType.Int })));
    }

    public bool PutDraft(TLDraftInfo draft)
    {
        var info = draft.AsDraftInfo();
        return _drafts.Put(draft.AsSpan().ToArray(), info.UserId, info.PeerType,
            info.PeerId, info.TopMsgId);
    }

    public async ValueTask<TLDraftInfo?> GetDraftAsync(long userId, int peerType,
        long peerId, int topMsgId)
    {
        byte[]? bytes = await _drafts.GetAsync(userId, peerType, peerId, topMsgId);
        return bytes is { Length: > 0 }
            ? new TLDraftInfo(bytes, 0, bytes.Length)
            : null;
    }

    public async ValueTask<IReadOnlyCollection<TLDraftInfo>> GetDraftsAsync(long userId)
    {
        List<TLDraftInfo> drafts = new();
        await foreach (byte[] bytes in _drafts.IterateAsync(userId))
        {
            drafts.Add(new TLDraftInfo(bytes, 0, bytes.Length));
        }
        return drafts;
    }

    public bool DeleteDraft(long userId, int peerType, long peerId, int topMsgId) =>
        _drafts.Delete(userId, peerType, peerId, topMsgId);

    public bool DeleteDrafts(long userId) => _drafts.Delete(userId);
}
