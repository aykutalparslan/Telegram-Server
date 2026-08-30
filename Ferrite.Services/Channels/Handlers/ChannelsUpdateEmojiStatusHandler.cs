// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.channels;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

public sealed class ChannelsUpdateEmojiStatusHandler : ChannelPropertyHandlerBase
{
    private readonly IChatRepository _chatRepository;

    public ChannelsUpdateEmojiStatusHandler(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChannelAdminRepository channelAdminRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,
        ICounterFactory counterFactory, IdAllocators ids,
        IUpdatesContextFactory updatesContextFactory, IUpdatesService updates,
        ISearchEngine search, IUploadService upload,
        IPhotoProcessingService photos, ILogger log, ChatRowStore chatRows,
        UpdateFanout fanout)
        : base(unitOfWork, channelMessagesRepository, authorizationRepository, channelAdminLogRepository, channelAdminRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, counterFactory, ids, updatesContextFactory, updates,
            search, upload, photos, log, chatRows, fanout)
    {
        _chatRepository = chatRepository;

    }

    [TLFunction(Constructors.baseLayer_ChannelsUpdateEmojiStatus)]
    public async Task<Ferrite.TL.baseLayer.TLUpdates> Handle(long authKeyId, TLBytes q)
    {
        var request = (ChannelsUpdateEmojiStatus)q;
        long? channelId = ResolveInputChannelId(request.Get_ChannelView());
        byte[] statusBytes = request.Get_EmojiStatusView().Constructor ==
                             Constructors.baseLayer_EmojiStatusEmpty
            ? Array.Empty<byte>()
            : request.EmojiStatus.ToArray();

        var (currentUserId, channelBytes, error) = await PrepareChannelMutation(
            authKeyId, channelId, creatorOnly: false,
            ChatAdminRightRequirement.ChangeInfo);
        if (error != null)
        {
            return error.Value;
        }

        long id = channelId!.Value;
        byte[] previousStatus = ReadEmojiStatus(channelBytes);
        byte[] updatedChannelBytes = StoreEmojiStatus(channelBytes, statusBytes);

        byte[] logAction;
        using (TLChannelAdminLogEventAction action =
               ChannelAdminLogEventActionChangeEmojiStatus.Builder()
                   .PrevValue(previousStatus)
                   .NewValue(ReadEmojiStatus(updatedChannelBytes))
                   .Build())
        {
            logAction = action.AsSpan().ToArray();
        }
        await AppendAdminLogEventAsync(id, currentUserId, logAction,
            (int)DateTimeOffset.Now.ToUnixTimeSeconds());

        _log.Debug($"📣 UpdateEmojiStatus user:{currentUserId} channel:{id} " +
                   $"set:{statusBytes.Length > 0}");
        return await CompleteAsync(authKeyId, currentUserId, id, updatedChannelBytes);
    }

    private static byte[] ReadEmojiStatus(byte[] channelBytes)
    {
        using var stored = new TLChat(channelBytes, 0, channelBytes.Length);
        Span<byte> value = stored.AsChannel().EmojiStatus;
        if (value.Length > 0)
        {
            return value.ToArray();
        }

        using TLEmojiStatus empty = EmojiStatusEmpty.Builder().Build();
        return empty.AsSpan().ToArray();
    }

    private byte[] StoreEmojiStatus(byte[] channelBytes, byte[] statusBytes)
    {
        using var stored = new TLChat(channelBytes, 0, channelBytes.Length);
        using TLChat updated = ChannelRows.WithEmojiStatus(stored.AsChannel(),
            statusBytes);
        _chatRepository.PutChat(updated);
        return updated.AsSpan().ToArray();
    }
}
