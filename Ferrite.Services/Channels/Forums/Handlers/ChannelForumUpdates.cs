// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;

namespace Ferrite.Services.Handlers.ChannelForums;

internal static class ChannelForumUpdates
{
    internal static async Task<(byte[] MessageBytes, int Pts)>
        WriteChannelServiceMessageAsync(IChannelMessagesRepository channelMessagesRepository,
            ICounterFactory counterFactory, long channelId, long actorUserId,
            byte[] actionBytes, int date, byte[]? replyToHeaderBytes = null,
            DialogPeerKey? sender = null)
    {
        var channelBox = new ChannelMessageBox(counterFactory, channelId);
        int messageId = await channelBox.NextMessageId();
        using TLPeer channelPeer = new PeerChannel(channelId);
        DialogPeerKey authoredPeer = sender ?? new DialogPeerKey(
            TLPeer.PeerType.PeerUser, actorUserId);
        using TLPeer actorPeer = PeerResolver.BuildPeer(authoredPeer.Type,
            authoredPeer.Id);
        var builder = MessageService.Builder()
            .Id(messageId)
            .FromId(actorPeer.AsSpan())
            .PeerId(channelPeer.AsSpan())
            .Date(date)
            .Action(actionBytes);
        if (replyToHeaderBytes is { Length: > 0 })
            builder = builder.ReplyTo(replyToHeaderBytes);
        using TLMessage serviceMessage = builder.Build();
        byte[] serviceMessageBytes = serviceMessage.AsSpan().ToArray();
        int pts = await channelBox.IncrementPts();
        channelMessagesRepository.PutMessage(channelId, serviceMessage, pts);
        return (serviceMessageBytes, pts);
    }

    internal static async Task<Ferrite.TL.baseLayer.TLUpdates> BuildForumResultAsync(
        IUnitOfWork unitOfWork, UpdateFanout fanout, long authKeyId,
        long actorUserId, byte[] channelBytes,
        IReadOnlyCollection<byte[]> updateBytes,
        IReadOnlyCollection<long>? extraChatIds = null)
    {
        await unitOfWork.SaveAsync();
        int date = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
        return await fanout.BuildForumResultAsync(authKeyId, actorUserId,
            channelBytes, updateBytes, date, extraChatIds);
    }

    internal static async Task<Ferrite.TL.baseLayer.TLUpdates> BuildChannelResultAsync(
        IUnitOfWork unitOfWork, UpdateFanout fanout, long authKeyId,
        long actorUserId, byte[] channelBytes,
        IReadOnlyCollection<long> extraUserIds)
    {
        await unitOfWork.SaveAsync();
        int date = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
        return await fanout.BuildChannelStateResultAsync(authKeyId, actorUserId,
            channelBytes, extraUserIds, date);
    }
}
