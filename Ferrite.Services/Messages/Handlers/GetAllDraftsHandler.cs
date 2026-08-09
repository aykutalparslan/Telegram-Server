// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class GetAllDraftsHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IDraftsRepository _draftsRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly DraftStore _drafts;
    private readonly UpdateFanout _fanout;

    public GetAllDraftsHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IDraftsRepository draftsRepository, DraftStore drafts,
        UpdateFanout fanout)
    {
        _authorizationRepository = authorizationRepository;
        _draftsRepository = draftsRepository;

        _unitOfWork = unitOfWork;
        _drafts = drafts;
        _fanout = fanout;
    }

    [TLFunction(Constructors.baseLayer_GetAllDrafts)]
    public async Task<TLUpdates> Handle(long authKeyId, TLBytes q)
    {
        using var auth = await _authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        if (auth == null)
        {
            return (TLUpdates)RpcErrorGenerator.GenerateError(400,
                "AUTH_KEY_INVALID"u8);
        }

        long userId = auth.Value.AsAuthInfo().UserId;
        IReadOnlyCollection<TLDraftInfo> rows = await _draftsRepository
            .GetDraftsAsync(userId);
        var snapshots = new List<DraftSnapshot>(rows.Count);
        foreach (TLDraftInfo row in rows)
        {
            using var owned = row;
            var info = owned.AsDraftInfo();
            snapshots.Add(new DraftSnapshot(
                new DraftAddress((TLPeer.PeerType)info.PeerType, info.PeerId,
                    info.TopMsgId),
                info.Draft.ToArray()));
        }
        snapshots.Sort(static (left, right) =>
        {
            int peerType = ((int)left.Address.PeerType)
                .CompareTo((int)right.Address.PeerType);
            if (peerType != 0) return peerType;
            int peer = left.Address.PeerId.CompareTo(right.Address.PeerId);
            return peer != 0 ? peer : left.Address.TopMsgId.CompareTo(
                right.Address.TopMsgId);
        });

        var relatedUserIds = new HashSet<long>();
        var relatedChatIds = new HashSet<long>();
        var updateBytes = new List<byte[]>(snapshots.Count);
        foreach (DraftSnapshot snapshot in snapshots)
        {
            if (snapshot.Address.PeerType == TLPeer.PeerType.PeerUser)
                relatedUserIds.Add(snapshot.Address.PeerId);
            else
                relatedChatIds.Add(snapshot.Address.PeerId);
            using TLUpdate update = DraftStore.BuildUpdate(snapshot.Address,
                snapshot.DraftBytes);
            updateBytes.Add(update.AsSpan().ToArray());
        }

        List<byte[]> chatBytes = await _fanout.GetChatBytesForViewerAsync(userId,
            relatedChatIds);
        var updates = new Vector();
        foreach (byte[] bytes in updateBytes) updates.AppendTLObject(bytes);
        var users = new Vector();
        _fanout.AppendUsers(ref users, relatedUserIds);
        var chats = new Vector();
        foreach (byte[] bytes in chatBytes) chats.AppendTLObject(bytes);
        return Updates.Builder().UpdatesProperty(updates).Users(users).Chats(chats)
            .Date(_drafts.CurrentDate).Seq(0).Build();
    }

    private sealed record DraftSnapshot(DraftAddress Address, byte[] DraftBytes);
}
