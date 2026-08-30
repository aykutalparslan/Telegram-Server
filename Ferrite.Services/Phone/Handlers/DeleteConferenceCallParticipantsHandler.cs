// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.Services.Calls;
using Ferrite.Services.Calls.E2E;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.phone;
using Ferrite.Utils;
using TLDto = Ferrite.TL.baseLayer.dto;
using TLUpdatesResult = Ferrite.TL.baseLayer.TLUpdates;

namespace Ferrite.Services.Phone.Handlers;

public sealed class DeleteConferenceCallParticipantsHandler : ConferenceCallHandlerBase
{
    private readonly IGroupCallsRepository _groupCallsRepository;

    private readonly IGroupCallMediaPlane _media;

    public DeleteConferenceCallParticipantsHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IAuthorizationRepository authorizationRepository, IGroupCallsRepository groupCallsRepository, IMessageRepository messageRepository,
        UpdateFanout fanout, GroupCallChatLink chatLink,
        IUpdatesContextFactory updatesContexts, IMTProtoTime time,
        GroupCallVideoOptions videoOptions, GroupCallMediaSourceMap sourceMap,
        ILogger log, IGroupCallChainService chain, IGroupCallMediaPlane media)
        : base(unitOfWork, chatParticipantsRepository, chatRepository, authorizationRepository, groupCallsRepository, messageRepository, fanout, chatLink, updatesContexts, time, videoOptions,
            sourceMap, log, chain)
    {
        _groupCallsRepository = groupCallsRepository;

        _media = media;
    }

    [TLFunction(Constructors.baseLayer_DeleteConferenceCallParticipants)]
    public async ValueTask<TLUpdatesResult> Handle(long authKeyId, TLBytes q)
    {
        var request = (DeleteConferenceCallParticipants)q;
        bool callRead = TryReadInputGroupCall(request.Get_CallView(), out long callId,
            out long accessHash);
        bool onlyLeft = request.OnlyLeft;
        List<long> ids = ReadIds(request.Ids);
        byte[] block = request.Block.ToArray();

        if (!callRead)
        {
            return Error(GroupCallErrors.GroupCallInvalid);
        }

        using ConferenceResolution resolution = await ResolveConferenceAsync(authKeyId,
            callId, accessHash);
        if (resolution.Error != null)
        {
            return Error(resolution.Error);
        }
        if (!resolution.IsParticipant)
        {
            return Error(GroupCallErrors.GroupCallJoinMissing);
        }

        ChainGroupStateValue? before = await Chain.GetGroupStateAsync(callId);
        if (before == null)
        {
            return Error(GroupCallErrors.BlockInvalid);
        }

        if (!TryReadRemovedUserIds(block, before, out List<long> removed))
        {
            return Error(GroupCallErrors.BlockInvalid);
        }
        if (!SameSet(removed, ids))
        {
            Log.Debug($"📞 deleteConferenceCallParticipants ids disagree with the " +
                      $"block for call:{callId}: block removes " +
                      $"[{string.Join(',', removed)}], request names " +
                      $"[{string.Join(',', ids)}]");
            return Error(GroupCallErrors.BlockInvalid);
        }

        if (onlyLeft)
        {
            foreach (long userId in ids)
            {
                if (await IsActiveParticipantAsync(callId, userId))
                {
                    return Error(GroupCallErrors.BlockInvalid);
                }
            }
        }

        GroupCallChainAppend appended = await Chain.TryAppendAsync(callId,
            GroupCallSubChain.Blocks, resolution.CurrentUserId, block);
        if (appended.Error != ChainValidationError.None)
        {
            Log.Debug($"📞 deleteConferenceCallParticipants rejected the block for " +
                      $"call:{callId} user:{resolution.CurrentUserId}: {appended.Error}");
            return Error(TranslateChainError(appended.Error));
        }

        var updates = new List<byte[]>();
        TLDto.TLGroupCallState? latestCall = null;
        foreach (long userId in ids)
        {
            string? mediaId = await GetMediaIdAsync(callId, userId);
            GroupCallLeaveResult left = await _groupCallsRepository
                .TryLeaveParticipantAsync(callId, userId);
            if (left.Status != GroupCallLeaveStatus.Left)
            {
                left.Participant?.Dispose();
                left.Call?.Dispose();
                continue;
            }

            if (mediaId != null)
            {
                await GroupCallJoinRows.ReleaseTransportAsync(_media, Log, callId,
                    mediaId, "removed conference participant");
                SourceMap.RemoveParticipant(callId, mediaId);
            }

            using TLDto.TLGroupCallParticipantState removedRow = left.Participant!.Value;
            latestCall?.Dispose();
            latestCall = left.Call!.Value;
            using TLGroupCallParticipant row = GroupCallBuilders.BuildParticipant(
                removedRow, new GroupCallViewer(resolution.CurrentUserId,
                    resolution.IsCreator, false), GroupCallParticipantOverlay.None,
                GroupCallParticipantDecoration.Versioned);
            using TLUpdate update = BuildParticipantsUpdate(latestCall.Value,
                row.AsSpan());
            updates.Add(update.AsSpan().ToArray());
            await PushRemovalAsync(latestCall.Value, removedRow,
                resolution.CurrentUserId);
        }

        await UnitOfWork.SaveAsync();

        byte[] blocksUpdate = await BuildChainBlocksBytesAsync(callId, accessHash,
            GroupCallSubChain.Blocks, offset: appended.Height,
            limit: GroupCallChainService.MaxWindow);
        updates.Add(blocksUpdate);
        List<long> members = await GetConferenceMemberIdsAsync(callId,
            resolution.CurrentUserId);
        await Fanout.EnqueueSerializedAsync(members, new[] { blocksUpdate });

        latestCall?.Dispose();
        Log.Debug($"📞 deleteConferenceCallParticipants call:{callId} " +
                  $"user:{resolution.CurrentUserId} removed:{ids.Count} " +
                  $"height:{appended.Height}");
        return await BuildConferenceResultAsync(authKeyId, resolution.CurrentUserId,
            updates, ids);
    }

    private static bool TryReadRemovedUserIds(byte[] block, ChainGroupStateValue before,
        out List<long> removed)
    {
        removed = new List<long>();
        if (!ChainBlockCodec.TryParse(block, out ChainBlockValue parsed, out _))
        {
            return false;
        }

        ChainGroupStateValue? after = null;
        foreach (ChainChangeValue change in parsed.Changes)
        {
            if (change is ChainChangeSetGroupStateValue setGroupState)
            {
                after = setGroupState.GroupState;
            }
        }
        if (after == null)
        {
            return true;
        }

        foreach (ChainParticipant participant in before.Participants)
        {
            if (after.FindByUserId(participant.UserId) == null)
            {
                removed.Add(participant.UserId);
            }
        }
        return true;
    }

    private static bool SameSet(IReadOnlyCollection<long> left,
        IReadOnlyCollection<long> right) =>
        left.Count == right.Distinct().Count() &&
        right.Distinct().All(left.Contains);

    private static List<long> ReadIds(VectorOfLong ids)
    {
        var result = new List<long>(ids.Count);
        for (int i = 0; i < ids.Count; i++)
        {
            result.Add(ids[i]);
        }
        return result;
    }

    private async Task PushRemovalAsync(TLDto.TLGroupCallState call,
        TLDto.TLGroupCallParticipantState removed, long invokerUserId)
    {
        var view = call.AsGroupCallState();
        long callId = view.Id;
        long creatorUserId = view.CreatorUserId;
        long removedUserId = removed.AsGroupCallParticipantState().UserId;
        List<long> members = await GetConferenceMemberIdsAsync(callId, invokerUserId);
        if (removedUserId != invokerUserId && !members.Contains(removedUserId))
        {
            members.Add(removedUserId);
        }

        await Fanout.PushGroupCallUpdatesToAsync(members, async memberId =>
        {
            GroupCallViewer viewer = await BuildViewerAsync(callId, memberId,
                memberId == creatorUserId);
            using TLGroupCallParticipant row = GroupCallBuilders.BuildParticipant(
                removed, viewer, GroupCallParticipantOverlay.None,
                GroupCallParticipantDecoration.Versioned);
            return BuildParticipantsUpdate(call, row.AsSpan());
        });
    }
}
