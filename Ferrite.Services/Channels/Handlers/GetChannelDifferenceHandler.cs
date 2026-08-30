// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using System.Text.RegularExpressions;
using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.channels;
using Ferrite.TL.baseLayer.dto;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

public sealed class GetChannelDifferenceHandler : ChannelsHandlerBase
{
    private readonly IChannelContentReadsRepository _channelContentReadsRepository;
    private readonly IChannelMessagesRepository _channelMessagesRepository;

    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IUserRepository _userRepository;

    public GetChannelDifferenceHandler(IUnitOfWork unitOfWork, IChannelContentReadsRepository channelContentReadsRepository, IChannelMessagesRepository channelMessagesRepository, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository, ICounterFactory counterFactory,
        IdAllocators ids, IUpdatesContextFactory updatesContextFactory,
        IUpdatesService updates, ISearchEngine search, IUploadService upload,
        IPhotoProcessingService photos, ILogger log, ChatRowStore chatRows,
        UpdateFanout fanout)
        : base(unitOfWork, channelMessagesRepository, authorizationRepository, channelAdminLogRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, counterFactory, ids, updatesContextFactory, updates,
            search, upload, photos, log, chatRows, fanout)
    {
        _channelContentReadsRepository = channelContentReadsRepository;
        _channelMessagesRepository = channelMessagesRepository;

        _authorizationRepository = authorizationRepository;
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;
        _userRepository = userRepository;

    }

    [TLFunction(Constructors.baseLayer_GetChannelDifference)]
    public async Task<Ferrite.TL.baseLayer.updates.TLChannelDifference> Handle(
        long authKeyId, TLBytes q)
    {
        var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
        if (auth == null)
        {
            return (Ferrite.TL.baseLayer.updates.TLChannelDifference)RpcErrorGenerator
                .GenerateError(400, "AUTH_KEY_INVALID"u8);
        }

        var request = (Ferrite.TL.baseLayer.updates.GetChannelDifference)q;
        long? channelId = ResolveInputChannelId(request.Get_ChannelView());
        int clientPts = request.Pts;
        int limit = request.Limit;
        if (channelId == null)
        {
            return (Ferrite.TL.baseLayer.updates.TLChannelDifference)RpcErrorGenerator
                .GenerateError(400, "CHANNEL_INVALID"u8);
        }

        var channelBox = new ChannelMessageBox(_counterFactory, channelId.Value);
        int publicationsAtEntry = await channelBox.PendingPtsPublications();
        bool publicationsSettled = await channelBox.WaitForPtsPublications();
        int reservedPts = await channelBox.Pts();
        int serverPts = publicationsSettled
            ? Math.Max(1, reservedPts - publicationsAtEntry)
            : reservedPts;
        if (clientPts >= serverPts)
        {
            _log.Debug($"📣 GetChannelDifference channel:{channelId.Value} clientPts:{clientPts} " +
                       $"serverPts:{serverPts} -> empty");
            return Ferrite.TL.baseLayer.updates.ChannelDifferenceEmpty.Builder()
                .Final(true)
                .Pts(serverPts)
                .Build();
        }

        var entries = new List<DifferenceEntry>();
        IReadOnlyCollection<TLSavedMessage> saved = await _channelMessagesRepository.GetMessagesByPtsAsync(channelId.Value,
                clientPts + 1, serverPts) ?? Array.Empty<TLSavedMessage>();
        foreach (TLSavedMessage savedMessage in saved)
        {
            using (savedMessage)
            {
                var view = savedMessage.AsSavedMessage();
                entries.Add(new DifferenceEntry(view.Pts, 1,
                    view.OriginalMessage.ToArray(), null));
            }
        }
        IReadOnlyCollection<TLUpdate> storedUpdates = await _channelMessagesRepository.GetUpdatesByPtsAsync(channelId.Value,
                clientPts + 1, serverPts) ?? Array.Empty<TLUpdate>();
        foreach (TLUpdate storedUpdate in storedUpdates)
        {
            using (storedUpdate)
            {
                (int pts, int ptsCount) = ReadUpdatePosition(storedUpdate);
                if (pts > 0 && ptsCount > 0)
                {
                    entries.Add(new DifferenceEntry(pts, ptsCount, null,
                        storedUpdate.AsSpan().ToArray()));
                }
            }
        }

        var window = new List<DifferenceEntry>();
        int resultPts = clientPts;
        int consumed = 0;
        foreach (DifferenceEntry entry in entries.OrderBy(entry => entry.Pts))
        {
            if (limit > 0 && window.Count > 0 &&
                consumed + entry.PtsCount > limit)
            {
                break;
            }
            window.Add(entry);
            resultPts = entry.Pts;
            consumed += entry.PtsCount;
        }
        bool truncated = window.Count != entries.Count;
        bool final = !truncated && resultPts >= serverPts;
        using var channel = await _chatRepository.GetChatAsync(channelId.Value);
        var participantInfos = await _chatParticipantsRepository
            .GetParticipantsAsync(channelId.Value);
        var activeParticipantIds = participantInfos.Where(IsActiveParticipant)
            .Select(p => p.AsChatParticipantInfo().UserId)
            .ToList();

        var relatedChatIds = new HashSet<long> { channelId.Value };
        foreach (DifferenceEntry entry in window)
        {
            if (entry.MessageBytes == null) continue;
            long actionChatId = ResolveMigrationActionChatId(entry.MessageBytes);
            if (actionChatId > 0)
            {
                relatedChatIds.Add(actionChatId);
            }
        }

        var relatedChatBytes = new List<byte[]>();
        foreach (long relatedChatId in relatedChatIds)
        {
            if (relatedChatId == channelId.Value && channel != null)
            {
                relatedChatBytes.Add(channel.Value.AsSpan().ToArray());
                continue;
            }
            using var relatedChat = await _chatRepository
                .GetChatAsync(relatedChatId);
            if (relatedChat != null)
            {
                relatedChatBytes.Add(relatedChat.Value.AsSpan().ToArray());
            }
        }

        string? callerUsername = null;
        long callerUserId = auth.Value.AsAuthInfo().UserId;
        using (TLUser? caller = _userRepository.GetUser(callerUserId))
        {
            if (caller != null && caller.Value.AsUser().Username.Length > 0)
            {
                callerUsername = Encoding.UTF8.GetString(
                    caller.Value.AsUser().Username);
            }
        }
        var projectedMessages = new List<byte[]>();
        foreach (DifferenceEntry entry in window)
        {
            if (entry.MessageBytes != null)
            {
                projectedMessages.Add(await ProjectMessageAsync(channelId.Value,
                    callerUserId, callerUsername, entry.MessageBytes));
            }
        }

        var newMessages = new Vector();
        foreach (byte[] messageBytes in projectedMessages)
        {
            newMessages.AppendTLObject(messageBytes);
        }
        var otherUpdates = new Vector();
        foreach (DifferenceEntry entry in window)
        {
            if (entry.UpdateBytes != null)
            {
                otherUpdates.AppendTLObject(entry.UpdateBytes);
            }
        }

        var chatVector = new Vector();
        foreach (byte[] relatedChatBytesItem in relatedChatBytes)
        {
            chatVector.AppendTLObject(relatedChatBytesItem);
        }

        var userVector = new Vector();
        AppendUsers(callerUserId, ref userVector, activeParticipantIds);

        _log.Debug($"📣 GetChannelDifference channel:{channelId.Value} clientPts:{clientPts} " +
                   $"serverPts:{serverPts} messages:{projectedMessages.Count} " +
                   $"updates:{window.Count - projectedMessages.Count} final:{final}");

        return Ferrite.TL.baseLayer.updates.ChannelDifference.Builder()
            .Final(final)
            .Pts(resultPts)
            .NewMessages(newMessages)
            .OtherUpdates(otherUpdates)
            .Chats(chatVector)
            .Users(userVector)
            .Build();
    }

    private async Task<byte[]> ProjectMessageAsync(long channelId, long userId,
        string? username, byte[] messageBytes)
    {
        int messageId;
        bool mentioned;
        using (var stored = new TLMessage(messageBytes, 0, messageBytes.Length))
        {
            if (stored.Type != TLMessage.MessageType.Message)
            {
                return messageBytes;
            }
            var message = stored.AsMessage();
            messageId = MessageIds.GetId(stored);
            mentioned = ResolveMessageSenderId(messageBytes) != userId &&
                        MessageMentions.MentionsUser(message, userId, username);
        }
        if (!mentioned)
        {
            return messageBytes;
        }

        using TLChannelContentRead? contentRead = await _channelContentReadsRepository.GetContentReadAsync(userId, channelId,
                messageId);
        return contentRead == null
            ? MessageMentions.StampUnread(messageBytes)
            : messageBytes;
    }

    private static (int Pts, int PtsCount) ReadUpdatePosition(TLUpdate update) =>
        update.Constructor switch
        {
            Constructors.baseLayer_UpdateDeleteChannelMessages =>
                (update.AsUpdateDeleteChannelMessages().Pts,
                    update.AsUpdateDeleteChannelMessages().PtsCount),
            Constructors.baseLayer_UpdateEditChannelMessage =>
                (update.AsUpdateEditChannelMessage().Pts,
                    update.AsUpdateEditChannelMessage().PtsCount),
            Constructors.baseLayer_UpdatePinnedChannelMessages =>
                (update.AsUpdatePinnedChannelMessages().Pts,
                    update.AsUpdatePinnedChannelMessages().PtsCount),
            _ => (0, 0)
        };

    private readonly record struct DifferenceEntry(int Pts, int PtsCount,
        byte[]? MessageBytes, byte[]? UpdateBytes);
}
