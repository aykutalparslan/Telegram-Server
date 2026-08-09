// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services;

public readonly record struct DraftAddress(TLPeer.PeerType PeerType, long PeerId,
    int TopMsgId);

public sealed class DraftStore
{
    private readonly IDraftsRepository _draftsRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IUpdatesService _updates;
    private readonly TimeProvider _timeProvider;

    public DraftStore(IUnitOfWork unitOfWork, IDraftsRepository draftsRepository, IUpdatesService updates,
        TimeProvider timeProvider)
    {
        _draftsRepository = draftsRepository;

        _unitOfWork = unitOfWork;
        _updates = updates;
        _timeProvider = timeProvider;
    }

    public int CurrentDate => checked((int)_timeProvider.GetUtcNow().ToUnixTimeSeconds());

    public async Task<bool> SaveAsync(long authKeyId, long userId,
        DraftAddress address, byte[] draftBytes)
    {
        using (TLDraftInfo row = DraftInfo.Builder()
                   .UserId(userId)
                   .PeerType((int)address.PeerType)
                   .PeerId(address.PeerId)
                   .TopMsgId(address.TopMsgId)
                   .Draft(draftBytes)
                   .Build())
        {
            if (!_draftsRepository.PutDraft(row))
            {
                return false;
            }
        }
        if (!await _unitOfWork.SaveAsync())
        {
            return false;
        }

        await BroadcastAsync(authKeyId, userId, address, draftBytes);
        return true;
    }

    public async Task<bool> DeleteAsync(long authKeyId, long userId,
        DraftAddress address, bool requireExisting)
    {
        if (requireExisting)
        {
            using TLDraftInfo? existing = await _draftsRepository
                .GetDraftAsync(userId, (int)address.PeerType, address.PeerId,
                    address.TopMsgId);
            if (existing == null)
            {
                return true;
            }
        }

        if (!_draftsRepository.DeleteDraft(userId,
                (int)address.PeerType, address.PeerId, address.TopMsgId) ||
            !await _unitOfWork.SaveAsync())
        {
            return false;
        }

        byte[] emptyBytes = BuildEmptyDraftBytes(CurrentDate);
        await BroadcastAsync(authKeyId, userId, address, emptyBytes);
        return true;
    }

    /// <summary>
    /// Clears the draft requested by a normalized messages.sendMessage only after
    /// its caller has committed the send or scheduled queue entry.
    /// </summary>
    public Task<bool> ClearAfterSendAsync(long authKeyId, long userId,
        TLPeer.PeerType peerType, long peerId, byte[] sendMessageBytes)
    {
        using var owned = new TLBytes(sendMessageBytes, 0, sendMessageBytes.Length);
        var request = (SendMessage)owned;
        if (!request.ClearDraft)
        {
            return Task.FromResult(true);
        }

        int topMsgId = request.Flags[0]
            ? ResolveTopMsgId(request.Get_ReplyToView())
            : 0;
        return DeleteAsync(authKeyId, userId,
            new DraftAddress(peerType, peerId, topMsgId), requireExisting: true);
    }

    public async Task<bool> ClearAllAsync(long authKeyId, long userId)
    {
        IReadOnlyCollection<TLDraftInfo> rows = await _draftsRepository
            .GetDraftsAsync(userId);
        var addresses = new List<DraftAddress>(rows.Count);
        foreach (TLDraftInfo row in rows)
        {
            using var owned = row;
            var info = owned.AsDraftInfo();
            addresses.Add(new DraftAddress((TLPeer.PeerType)info.PeerType,
                info.PeerId, info.TopMsgId));
        }

        if (!_draftsRepository.DeleteDrafts(userId) ||
            !await _unitOfWork.SaveAsync())
        {
            return false;
        }

        byte[] emptyBytes = BuildEmptyDraftBytes(CurrentDate);
        foreach (DraftAddress address in addresses)
        {
            await BroadcastAsync(authKeyId, userId, address, emptyBytes);
        }
        return true;
    }

    public static bool IsEmpty(SaveDraft request)
    {
        Flags flags = request.Flags;
        return request.Message.IsEmpty && !flags[3] && !flags[4] && !flags[5] &&
               !flags[7] && !flags[8];
    }

    public static int ResolveTopMsgId(InputReplyToView replyTo)
    {
        if (replyTo.Is(out InputReplyToMessage message) && message.Flags[0])
        {
            return message.TopMsgId;
        }
        return 0;
    }

    public static byte[] BuildDraftBytes(SaveDraft request, int date)
    {
        var builder = DraftMessage.Builder()
            .NoWebpage(request.NoWebpage)
            .InvertMedia(request.InvertMedia)
            .Message(request.Message)
            .Date(date);
        Flags flags = request.Flags;
        if (flags[4]) builder = builder.ReplyTo(request.ReplyTo);
        if (flags[3]) builder = builder.Entities(request.Entities);
        if (flags[5]) builder = builder.Media(request.Media);
        if (flags[7]) builder = builder.Effect(request.Effect);
        if (flags[8]) builder = builder.SuggestedPost(request.SuggestedPost);
        using TLDraftMessage draft = builder.Build();
        return draft.AsSpan().ToArray();
    }

    public static byte[] BuildEmptyDraftBytes(int date)
    {
        using TLDraftMessage draft = DraftMessageEmpty.Builder().Date(date).Build();
        return draft.AsSpan().ToArray();
    }

    public static TLUpdate BuildUpdate(DraftAddress address,
        ReadOnlySpan<byte> draftBytes)
    {
        using TLPeer peer = PeerResolver.BuildPeer(address.PeerType, address.PeerId);
        var builder = UpdateDraftMessage.Builder()
            .Peer(peer.AsSpan())
            .Draft(draftBytes);
        if (address.TopMsgId > 0)
        {
            builder = builder.TopMsgId(address.TopMsgId);
        }
        return builder.Build();
    }

    private async Task BroadcastAsync(long authKeyId, long userId,
        DraftAddress address, byte[] draftBytes)
    {
        TLUpdate update = BuildUpdate(address, draftBytes);
        await _updates.EnqueueUpdate(userId, update,
            UpdateDeliveryScope.ExcludingAuthKeys(new[] { authKeyId }));
    }
}
