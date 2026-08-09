// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

public abstract class ChannelStickerHandlerBase
{
    private readonly IChatParticipantsRepository _chatParticipantsRepository;

    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IStickerRepository _stickerRepository;

    protected readonly record struct StickerSetInput(bool Empty, long? Id,
        long? AccessHash, string? ShortName);

    private readonly IUnitOfWork _unitOfWork;
    private readonly UpdateFanout _fanout;
    private readonly ILogger _log;

    protected ChannelStickerHandlerBase(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IAuthorizationRepository authorizationRepository, IChatRepository chatRepository, IStickerRepository stickerRepository,
        UpdateFanout fanout, ILogger log)
    {
        _chatParticipantsRepository = chatParticipantsRepository;

        _authorizationRepository = authorizationRepository;
        _chatRepository = chatRepository;
        _stickerRepository = stickerRepository;

        _unitOfWork = unitOfWork;
        _fanout = fanout;
        _log = log;
    }

    protected static long? ReadChannel(InputChannelView input)
    {
        if (input.Is(out InputChannel channel)) return channel.ChannelId;
        if (input.Is(out InputChannelFromMessage message)) return message.ChannelId;
        return null;
    }

    protected static StickerSetInput ReadSet(InputStickerSetView input)
    {
        if (input.Is(out InputStickerSetEmpty _))
            return new StickerSetInput(true, null, null, null);
        var value = StickerStore.ReadInputSet(input);
        return new StickerSetInput(false, value.Id, value.AccessHash,
            value.ShortName);
    }

    protected async Task<TLBool> SetAsync(long authKeyId, long? channelId,
        StickerSetInput input, bool emoji)
    {
        using TLAuthInfo? auth = await _authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        if (auth is null) return Error(400, "AUTH_KEY_INVALID"u8);
        long userId = auth.Value.AsAuthInfo().UserId;
        if (channelId is not > 0) return Error(400, "CHANNEL_INVALID"u8);

        using TLChat? channel = await _chatRepository
            .GetChatAsync(channelId.Value);
        if (channel is null || channel.Value.Type != TLChat.ChatType.Channel ||
            !channel.Value.AsChannel().Megagroup)
            return Error(400, "CHANNEL_INVALID"u8);
        using TLChatParticipantInfo? participant = await _chatParticipantsRepository.GetParticipantAsync(channelId.Value,
                userId);
        if (participant is null || !IsActive(participant.Value))
            return Error(400, "USER_NOT_PARTICIPANT"u8);
        if (!ChatRights.HasAdminRight(participant.Value,
                ChatAdminRightRequirement.ChangeInfo))
            return Error(400, "CHAT_ADMIN_REQUIRED"u8);

        long selectedId = 0;
        if (!input.Empty)
        {
            if (!input.Id.HasValue && input.ShortName is null)
                return Error(400, "STICKERSET_INVALID"u8);
            using TLStickerSetState? set = input.Id.HasValue
                ? await _stickerRepository.GetSetAsync(input.Id.Value)
                : await _stickerRepository
                    .GetSetByShortNameAsync(input.ShortName!);
            if (set is null) return Error(400, "STICKERSET_INVALID"u8);
            var setState = set.Value.AsStickerSetState();
            StickerSet value = setState.Get_SetView().AsStickerSet();
            if (input.Id.HasValue && input.AccessHash != value.AccessHash ||
                emoji != value.Emojis || !emoji && value.Masks)
                return Error(400, "STICKERSET_INVALID"u8);
            selectedId = setState.SetId;
        }

        using TLChannelStickerState? existing = await _stickerRepository.GetChannelStateAsync(channelId.Value);
        long normalId = existing?.AsChannelStickerState().StickerSetId ?? 0;
        long emojiId = existing?.AsChannelStickerState().EmojiSetId ?? 0;
        long currentId = emoji ? emojiId : normalId;
        if (currentId == selectedId) return new BoolTrue();
        if (emoji) emojiId = selectedId;
        else normalId = selectedId;

        bool stored;
        if (normalId == 0 && emojiId == 0)
        {
            stored = existing is null || _stickerRepository
                .DeleteChannelState(channelId.Value);
        }
        else
        {
            using TLChannelStickerState updated = ChannelStickerState.Builder()
                .ChannelId(channelId.Value).StickerSetId(normalId)
                .EmojiSetId(emojiId)
                .Date((int)DateTimeOffset.UtcNow.ToUnixTimeSeconds()).Build();
            stored = _stickerRepository.PutChannelState(updated);
        }
        if (!stored || !await _unitOfWork.SaveAsync())
            return Error(500, "STORAGE_FAILED"u8);
        await _fanout.PushUpdateChannelToOtherMembersAsync(channelId.Value,
            userId);
        _log.Debug($"📣 SetChannel{(emoji ? "Emoji" : "Normal")}Stickers " +
                   $"user:{userId} channel:{channelId.Value} set:{selectedId}");
        return new BoolTrue();
    }

    private static bool IsActive(TLChatParticipantInfo participant)
    {
        int role = participant.AsChatParticipantInfo().Role;
        return role != (int)ChatParticipantRole.Banned &&
               role != (int)ChatParticipantRole.Left;
    }

    private static TLBool Error(int code, ReadOnlySpan<byte> message) =>
        (TLBool)RpcErrorGenerator.GenerateError(code, message);
}
