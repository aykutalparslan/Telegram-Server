// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Data.Search;
using Ferrite.Services.Channels;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.channels;
using Ferrite.TL.baseLayer.dto;
using Ferrite.Utils;

namespace Ferrite.Services.Handlers.Channels;

/// <summary>
/// Links a broadcast channel to a discussion supergroup, or unlinks one.
///
/// The linkage is SYMMETRIC and both halves are load-bearing. `linked_chat_id`
/// lives in each side's durable administration row and `has_link` on each side's
/// compact channel row, because pinned TDLib derives two separate facts from
/// them: `channelFull.linked_chat_id` drives the discussion button, while
/// `get_channel_join_to_send` (`ChatManager.cpp:8443`) reports `join_to_send`
/// as TRUE for any megagroup WITHOUT a linked channel whatever the stored flag
/// says. So a one-sided write leaves the supergroup permanently claiming
/// join-to-send is on.
///
/// `inputChannelEmpty` in either position is the unlink route
/// (`ChatManager.cpp:3580,3610`): an empty group unlinks the named broadcast,
/// and an empty broadcast unlinks the named group from whatever it is attached
/// to.
/// </summary>
public sealed class SetDiscussionGroupHandler : ChannelPropertyHandlerBase
{
    private readonly IChannelAdminRepository _channelAdminRepository;
    private readonly IChatRepository _chatRepository;

    public SetDiscussionGroupHandler(IUnitOfWork unitOfWork, IChannelMessagesRepository channelMessagesRepository, IAuthorizationRepository authorizationRepository, IChannelAdminLogRepository channelAdminLogRepository, IChannelAdminRepository channelAdminRepository, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IMessageRepository messageRepository, IUserRepository userRepository,
        ICounterFactory counterFactory, IdAllocators ids,
        IUpdatesContextFactory updatesContextFactory, IUpdatesService updates,
        ISearchEngine search, IUploadService upload,
        IPhotoProcessingService photos, ILogger log, ChatRowStore chatRows,
        UpdateFanout fanout)
        : base(unitOfWork, channelMessagesRepository, authorizationRepository, channelAdminLogRepository, channelAdminRepository, chatParticipantsRepository, chatRepository, messageRepository, userRepository, counterFactory, ids, updatesContextFactory, updates,
            search, upload, photos, log, chatRows, fanout)
    {
        _channelAdminRepository = channelAdminRepository;
        _chatRepository = chatRepository;

    }

    [TLFunction(Constructors.baseLayer_SetDiscussionGroup)]
    public async Task<TLBool> Handle(long authKeyId, TLBytes q)
    {
        var request = (SetDiscussionGroup)q;
        long? broadcastId = ResolveInputChannelId(request.Get_BroadcastView());
        long? groupId = ResolveInputChannelId(request.Get_GroupView());
        if (broadcastId is null && groupId is null)
        {
            return ErrorBool("CHANNEL_INVALID"u8);
        }

        long actorUserId = 0;
        byte[]? broadcastBytes = null;
        if (broadcastId is not null)
        {
            var (currentUserId, channelBytes, error) = await PrepareChannelMutationCore(
                authKeyId, broadcastId, creatorOnly: false,
                ChatAdminRightRequirement.ChangeInfo);
            if (error != null)
            {
                return ErrorBool(Encoding.UTF8.GetBytes(error));
            }
            if (!ReadChannelFacts(channelBytes).Broadcast)
            {
                return ErrorBool("BROADCAST_REQUIRED"u8);
            }

            actorUserId = currentUserId;
            broadcastBytes = channelBytes;
        }

        byte[]? groupBytes = null;
        if (groupId is not null)
        {
            // Pinned TDLib requires administrator + can_pin_messages on the
            // discussion side (`ChatManager.cpp:3602`), not merely change-info.
            var (currentUserId, channelBytes, error) = await PrepareChannelMutationCore(
                authKeyId, groupId, creatorOnly: false,
                ChatAdminRightRequirement.PinMessages);
            if (error != null)
            {
                return ErrorBool(Encoding.UTF8.GetBytes(error));
            }
            if (!ReadChannelFacts(channelBytes).Megagroup)
            {
                return ErrorBool("MEGAGROUP_REQUIRED"u8);
            }

            actorUserId = currentUserId;
            groupBytes = channelBytes;
        }

        int date = (int)DateTimeOffset.Now.ToUnixTimeSeconds();
        if (broadcastId is not null && groupId is not null)
        {
            // A discussion group's history has to be readable by everyone who
            // arrives from the channel, so a hidden pre-history refuses the link
            // rather than silently producing a group new members cannot read.
            using (TLChannelAdminState groupState =
                   await LoadAdminStateAsync(groupId.Value, date))
            {
                if (groupState.AsChannelAdminState().HiddenPrehistory)
                {
                    return ErrorBool("MEGAGROUP_PREHISTORY_HIDDEN"u8);
                }
            }

            long existing = await ReadLinkedChatIdAsync(broadcastId.Value);
            if (existing == groupId.Value)
            {
                return ErrorBool("LINK_NOT_MODIFIED"u8);
            }
            if (existing != 0)
            {
                await UnlinkAsync(existing, actorUserId, date);
            }

            long groupExisting = await ReadLinkedChatIdAsync(groupId.Value);
            if (groupExisting != 0 && groupExisting != broadcastId.Value)
            {
                await UnlinkAsync(groupExisting, actorUserId, date);
            }

            await LinkAsync(broadcastId.Value, broadcastBytes!, groupId.Value, actorUserId, date);
            await LinkAsync(groupId.Value, groupBytes!, broadcastId.Value, actorUserId, date);
            await _unitOfWork.SaveAsync();
            await _fanout.PushUpdateChannelToOtherMembersAsync(broadcastId.Value, actorUserId);
            await _fanout.PushUpdateChannelToOtherMembersAsync(groupId.Value, actorUserId);
            _log.Debug($"📣 SetDiscussionGroup user:{actorUserId} " +
                       $"broadcast:{broadcastId.Value} group:{groupId.Value}");
            return new BoolTrue();
        }

        // One side named alone is the unlink route: whichever channel was given
        // is detached from its counterpart, and the counterpart is cleared too.
        long subjectId = broadcastId ?? groupId!.Value;
        long counterpartId = await ReadLinkedChatIdAsync(subjectId);
        if (counterpartId == 0)
        {
            return ErrorBool("LINK_NOT_MODIFIED"u8);
        }

        await UnlinkAsync(subjectId, actorUserId, date);
        await _unitOfWork.SaveAsync();
        await _fanout.PushUpdateChannelToOtherMembersAsync(subjectId, actorUserId);
        await _fanout.PushUpdateChannelToOtherMembersAsync(counterpartId, actorUserId);
        _log.Debug($"📣 SetDiscussionGroup user:{actorUserId} unlinked:{subjectId} " +
                   $"from:{counterpartId}");
        return new BoolTrue();
    }

    private async Task<long> ReadLinkedChatIdAsync(long channelId)
    {
        using TLChannelAdminState? state = await _channelAdminRepository.GetStateAsync(channelId);
        return state?.AsChannelAdminState().LinkedChatId ?? 0;
    }

    // Writes ONE side of the linkage: the durable id plus the `has_link` flag
    // the compact row carries.
    private async Task LinkAsync(long channelId, byte[] channelBytes,
        long linkedChatId, long actorUserId, int date)
    {
        long previousLinkedChatId;
        using (TLChannelAdminState state = await LoadAdminStateAsync(channelId, date))
        {
            var view = state.AsChannelAdminState();
            previousLinkedChatId = view.LinkedChatId;
            using TLChannelAdminState updated =
                ChannelAdminStateRows.WithLinkedChatId(view, linkedChatId, date);
            _channelAdminRepository.PutState(updated);
        }

        StoreChannelFlags(channelBytes, flagBit: 20, flagValue: true);
        await AppendLinkedChatEventAsync(channelId, actorUserId,
            previousLinkedChatId, linkedChatId, date);
    }

    // Clears both halves of whatever linkage the named channel is part of, so an
    // unlink can never leave one side pointing at the other.
    private async Task UnlinkAsync(long channelId, long actorUserId, int date)
    {
        long counterpartId = await ReadLinkedChatIdAsync(channelId);
        await ClearLinkAsync(channelId, actorUserId, date);
        if (counterpartId != 0)
        {
            await ClearLinkAsync(counterpartId, actorUserId, date);
        }
    }

    private async Task ClearLinkAsync(long channelId, long actorUserId, int date)
    {
        long previousLinkedChatId;
        using (TLChannelAdminState state = await LoadAdminStateAsync(channelId, date))
        {
            var view = state.AsChannelAdminState();
            previousLinkedChatId = view.LinkedChatId;
            using TLChannelAdminState updated =
                ChannelAdminStateRows.WithLinkedChatId(view, 0, date);
            _channelAdminRepository.PutState(updated);
        }

        using TLChat? stored = await _chatRepository.GetChatAsync(channelId);
        if (stored is { Type: TLChat.ChatType.Channel })
        {
            StoreChannelFlags(stored.Value.AsSpan().ToArray(), flagBit: 20,
                flagValue: false);
        }

        await AppendLinkedChatEventAsync(channelId, actorUserId,
            previousLinkedChatId, 0, date);
    }

    // Both sides of a link change get their own event, because each channel's log
    // is its own. An unchanged value is NOT recorded: pinned TDLib drops an event
    // whose prev and new linked chat are equal and logs an error for it
    // (`DialogEventLog.cpp:265-267`), so recording one would be invisible noise.
    private async Task AppendLinkedChatEventAsync(long channelId, long actorUserId,
        long previousLinkedChatId, long linkedChatId, int date)
    {
        if (previousLinkedChatId == linkedChatId)
        {
            return;
        }

        byte[] logAction;
        using (TLChannelAdminLogEventAction action =
               ChannelAdminLogEventActionChangeLinkedChat.Builder()
                   .PrevValue(previousLinkedChatId)
                   .NewValue(linkedChatId)
                   .Build())
        {
            logAction = action.AsSpan().ToArray();
        }
        await AppendAdminLogEventAsync(channelId, actorUserId, logAction, date);
    }
}
