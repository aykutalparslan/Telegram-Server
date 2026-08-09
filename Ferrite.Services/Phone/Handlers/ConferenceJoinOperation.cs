// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Services.Calls;
using Ferrite.Services.Calls.E2E;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.Utils;
using TLDto = Ferrite.TL.baseLayer.dto;
using TLUpdatesResult = Ferrite.TL.baseLayer.TLUpdates;

namespace Ferrite.Services.Phone.Handlers;

/// <summary>
/// The E2E half of a join, shared by phone.joinGroupCall's flags.3 branch and by
/// phone.createConferenceCall's inline join. It is one implementation on purpose:
/// the client cannot tell the two apart, and a second copy of the fork-prevention
/// ordering would be a second chance to get it wrong.
/// </summary>
public sealed class ConferenceJoinOperation : ConferenceCallHandlerBase
{
    private readonly IGroupCallsRepository _groupCallsRepository;

    private readonly IGroupCallMediaPlane _media;

    public ConferenceJoinOperation(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IAuthorizationRepository authorizationRepository, IGroupCallsRepository groupCallsRepository, IMessageRepository messageRepository, UpdateFanout fanout,
        GroupCallChatLink chatLink, IUpdatesContextFactory updatesContexts,
        IMTProtoTime time, GroupCallVideoOptions videoOptions,
        GroupCallMediaSourceMap sourceMap, ILogger log, IGroupCallChainService chain,
        IGroupCallMediaPlane media)
        : base(unitOfWork, chatParticipantsRepository, chatRepository, authorizationRepository, groupCallsRepository, messageRepository, fanout, chatLink, updatesContexts, time, videoOptions,
            sourceMap, log, chain)
    {
        _groupCallsRepository = groupCallsRepository;

        _media = media;
    }

    /// <summary>
    /// Joins a conference the caller resolved itself. The chain is the
    /// fork-prevention point, so the block is appended after the transport exists
    /// and before the participant is committed: a client that loses the height
    /// gets BLOCK_HEIGHT_MISMATCH with no participant, no transport and no chain
    /// growth, and its retry against the real head is safe.
    /// </summary>
    public async ValueTask<TLUpdatesResult> JoinAsync(long authKeyId,
        ConferenceCallRef reference, byte[] publicKey, byte[] block, byte[] paramsJson,
        bool requestedMuted, bool videoStopped)
    {
        using ConferenceResolution resolution = await ResolveConferenceAsync(authKeyId,
            reference);
        if (resolution.Error != null)
        {
            return Error(resolution.Error);
        }

        return await JoinResolvedAsync(resolution.Call!.Value, resolution.CurrentUserId,
            resolution.IsCreator, resolution.AccessHash, publicKey, block, paramsJson,
            requestedMuted, videoStopped);
    }

    /// <summary>
    /// The same join against a call the caller has already resolved and owns.
    /// createConferenceCall uses this so its brand-new row is not re-read, and so
    /// the result it builds can lead with the updateGroupCall that teaches the
    /// client the new call id.
    /// </summary>
    public async ValueTask<ConferenceJoinOutcome> JoinResolvedForResultAsync(
        TLDto.TLGroupCallState call, long userId, bool isCreator, long accessHash,
        byte[] publicKey, byte[] block, byte[] paramsJson, bool requestedMuted,
        bool videoStopped)
    {
        ConferenceJoinOutcome outcome = await RunJoinAsync(call, userId, isCreator,
            accessHash, publicKey, block, paramsJson, requestedMuted, videoStopped);
        return outcome;
    }

    private async ValueTask<TLUpdatesResult> JoinResolvedAsync(TLDto.TLGroupCallState call,
        long userId, bool isCreator, long accessHash, byte[] publicKey, byte[] block,
        byte[] paramsJson, bool requestedMuted, bool videoStopped)
    {
        ConferenceJoinOutcome outcome = await RunJoinAsync(call, userId, isCreator,
            accessHash, publicKey, block, paramsJson, requestedMuted, videoStopped);
        if (outcome.Error != null)
        {
            return Error(outcome.Error);
        }

        // Like every join answer, this carries the caller's own media credentials
        // and must be applied unconditionally; see BuildUnsequencedResultAsync.
        return BuildUnsequencedConferenceResult(userId, outcome.Updates);
    }

    private async ValueTask<ConferenceJoinOutcome> RunJoinAsync(TLDto.TLGroupCallState call,
        long userId, bool isCreator, long accessHash, byte[] publicKey, byte[] block,
        byte[] paramsJson, bool requestedMuted, bool videoStopped)
    {
        // The public key is what the block's signature is checked against inside
        // the chain, so an absent or short one cannot produce a valid block.
        if (publicKey.Length != 32 || block.Length == 0)
        {
            return ConferenceJoinOutcome.Failed(GroupCallErrors.BlockInvalid);
        }

        var view = call.AsGroupCallState();
        long callId = view.Id;
        bool callJoinMuted = view.JoinMuted;

        GroupCallJoinPayload payload;
        try
        {
            payload = GroupCallJoinPayloadCodec.ParseJoinPayload(paramsJson);
        }
        catch (GroupCallDataJsonException e)
        {
            Log.Debug($"📞 conference join rejected the payload for call:{callId} " +
                      $"user:{userId}: {e.Message}");
            return ConferenceJoinOutcome.Failed(GroupCallErrors.DataJsonInvalid);
        }

        // media_id is the durable correlation between a participant row and its
        // worker transport, so a rejoin keeps the same id.
        string mediaId;
        bool rejoining;
        using (TLDto.TLGroupCallParticipantState? existing = await _groupCallsRepository.GetParticipantAsync(callId, userId))
        {
            rejoining = existing != null;
            mediaId = existing != null
                ? Encoding.UTF8.GetString(
                    existing.Value.AsGroupCallParticipantState().MediaId)
                : Guid.NewGuid().ToString("N");
        }

        if (rejoining)
        {
            await GroupCallJoinRows.ReleaseTransportAsync(_media, Log, callId, mediaId,
                "stale rejoin transport");
        }

        GroupCallMediaJoinResult joined;
        try
        {
            // Idempotent: the room is allocated at create, but a worker that has
            // restarted since then no longer has it.
            await _media.CreateRoomAsync(callId);
            joined = await _media.JoinAsync(new GroupCallMediaJoinRequest(callId, mediaId,
                payload));
        }
        catch (GroupCallMediaException e)
        {
            Log.Warning(e, $"📞 conference join media failed for call:{callId} " +
                           $"user:{userId} kind:{e.Kind}");
            return ConferenceJoinOutcome.Failed(
                GroupCallJoinRows.TranslateMediaFailure(e.Kind));
        }

        GroupCallChainAppend appended = await Chain.TryAppendAsync(callId,
            GroupCallSubChain.Blocks, userId, block);
        if (appended.Error != ChainValidationError.None)
        {
            await GroupCallJoinRows.ReleaseTransportAsync(_media, Log, callId, mediaId,
                "rejected conference block");
            Log.Debug($"📞 conference join rejected the block for call:{callId} " +
                      $"user:{userId}: {appended.Error}");
            return ConferenceJoinOutcome.Failed(TranslateChainError(appended.Error));
        }

        int now = Now();
        bool muted = requestedMuted || (callJoinMuted && !isCreator);
        bool canSelfUnmute = muted && (isCreator || !callJoinMuted);
        bool videoJoined = payload.VideoSourceGroups.Count > 0 && !videoStopped;
        string? videoEndpoint = payload.VideoSourceGroups.Count > 0
            ? joined.Transport.Video?.Endpoint
            : null;

        GroupCallJoinResult stored;
        using (TLDto.TLGroupCallParticipantState row = GroupCallJoinRows
                   .BuildParticipantRow(callId, userId, mediaId, payload.Source, now,
                       muted, canSelfUnmute, videoJoined, videoStopped, videoEndpoint))
        {
            stored = await _groupCallsRepository.TryJoinParticipantAsync(row);
        }

        if (stored.Status is not (GroupCallJoinStatus.Joined or GroupCallJoinStatus.Rejoined))
        {
            stored.Participant?.Dispose();
            stored.Call?.Dispose();
            await GroupCallJoinRows.ReleaseTransportAsync(_media, Log, callId, mediaId,
                "uncommitted conference join");
            return ConferenceJoinOutcome.Failed(
                GroupCallJoinRows.TranslateJoinFailure(stored.Status));
        }

        await UnitOfWork.SaveAsync();
        SourceMap.Replace(callId, joined.ViewerSources);

        using TLDto.TLGroupCallParticipantState participant = stored.Participant!.Value;
        using TLDto.TLGroupCallState updatedCall = stored.Call!.Value;
        int videoCount = await CountUnmutedVideoAsync(callId);

        // The container order is load-bearing and is the reverse of what reads
        // naturally. TDLib keeps the connection params pending, buffers every
        // chain-blocks update that arrives WHILE they are pending, and then
        // consumes both when it processes updateGroupCall
        // (GroupCallManager.cpp:5284 and :5512). So the connection must lead, the
        // two sub-chains must follow it, and updateGroupCall must come last: a
        // container that leads with the call update makes the client apply the
        // join before it has a single block, log "Have no blocks", and leave the
        // conference unencrypted with no server-side error to notice.
        var updates = new List<byte[]>(5);
        byte[] connectionParams = GroupCallJoinPayloadCodec.BuildConnectionParams(
            joined.Transport);
        using (TLUpdate connection = GroupCallBuilders.BuildConnectionUpdate(
                   connectionParams))
        {
            updates.Add(connection.AsSpan().ToArray());
        }

        // Sub-chain 0 starts at the block this join just appended and runs
        // contiguously from there. TDLib calls call_create(blocks[0][0]) and
        // call_apply_block for the rest, and call_create rejects any block whose
        // group state does not already contain the caller's key
        // (Call::update_group_shared_key -> InvalidCallGroupState_NotParticipant).
        // So the window may not start at height 0 for anyone but the creator: an
        // earlier block would leave the client joined with no tde2e call at all,
        // which later aborts the whole process inside its own reconciliation.
        // Sub-chain 1 is always sent too, as its head alone, so the client learns
        // the right next offset even when there is nothing to apply yet.
        updates.Add(await BuildChainBlocksBytesAsync(callId, accessHash,
            GroupCallSubChain.Blocks, offset: appended.Height,
            limit: GroupCallChainService.MaxWindow));
        updates.Add(await BuildChainBlocksBytesAsync(callId, accessHash,
            GroupCallSubChain.Broadcast, offset: -1, limit: 1));

        GroupCallViewer viewer = await BuildViewerAsync(callId, userId, isCreator);
        var selfOverlay = new GroupCallParticipantOverlay(MutedByYou: false,
            LocalVolume: null, joined.CanonicalSource,
            GroupCallJoinRows.BuildSelfSources(joined, payload, videoJoined));
        using (TLGroupCallParticipant selfRow = GroupCallBuilders.BuildParticipant(
                   participant, viewer, selfOverlay,
                   GroupCallParticipantDecoration.JustJoined |
                   GroupCallParticipantDecoration.Versioned))
        using (TLUpdate participants = BuildParticipantsUpdate(updatedCall,
                   selfRow.AsSpan()))
        {
            updates.Add(participants.AsSpan().ToArray());
        }
        updates.Add(BuildConferenceCallUpdateBytes(updatedCall, viewer, videoCount));

        int delivered = await PushConferenceJoinAsync(updatedCall, participant, userId,
            accessHash, appended.Height, videoCount);

        Log.Debug($"📞 conference join call:{callId} user:{userId} " +
                  $"source:{payload.Source} media:{mediaId} rejoin:{rejoining} " +
                  $"height:{appended.Height} fanout:{delivered}");
        return ConferenceJoinOutcome.Succeeded(updates);
    }

    /// <summary>
    /// Every other joined participant learns about the new participant, the new
    /// call version, and the block that admitted them. The block matters: a
    /// member that only polled would stay on the old group state until its next
    /// poll and could not decrypt the joiner.
    /// </summary>
    private async Task<int> PushConferenceJoinAsync(TLDto.TLGroupCallState call,
        TLDto.TLGroupCallParticipantState participant, long joinerUserId,
        long accessHash, int height, int videoCount)
    {
        var view = call.AsGroupCallState();
        long callId = view.Id;
        long creatorUserId = view.CreatorUserId;
        string producerMediaId = Encoding.UTF8.GetString(
            participant.AsGroupCallParticipantState().MediaId);
        List<long> members = await GetConferenceMemberIdsAsync(callId, joinerUserId);

        int delivered = await Fanout.PushGroupCallUpdatesToAsync(members,
            async memberId =>
            {
                GroupCallViewer viewer = await BuildViewerAsync(callId, memberId,
                    memberId == creatorUserId);
                string? viewerMediaId = await GetMediaIdAsync(callId, memberId);
                GroupCallParticipantOverlay overlay = await BuildMemberOverlayAsync(
                    callId, memberId, viewerMediaId, joinerUserId, producerMediaId);
                using TLGroupCallParticipant row = GroupCallBuilders.BuildParticipant(
                    participant, viewer, overlay,
                    GroupCallParticipantDecoration.JustJoined |
                    GroupCallParticipantDecoration.Versioned);
                return BuildParticipantsUpdate(call, row.AsSpan());
            });

        await Fanout.PushGroupCallUpdatesToAsync(members, async memberId =>
        {
            GroupCallViewer viewer = await BuildViewerAsync(callId, memberId,
                memberId == creatorUserId);
            using TLGroupCall built = GroupCallBuilders.BuildCall(call, viewer,
                VideoOptions, videoCount);
            return UpdateGroupCall.Builder().Call(built.AsSpan()).Build();
        });

        // One window ending at the new head: TDLib applies the last
        // (next_offset - its own next offset) entries, so a member that is only
        // one block behind applies exactly this block.
        byte[] blocksUpdate = await BuildChainBlocksBytesAsync(callId, accessHash,
            GroupCallSubChain.Blocks, offset: height,
            limit: GroupCallChainService.MaxWindow);
        await Fanout.EnqueueSerializedAsync(members, new[] { blocksUpdate });
        return delivered;
    }
}

/// <summary>
/// A conference join's result as bytes, so the caller can decide what leads the
/// Updates container: a join answer leads with the connection, while a create
/// leads with the updateGroupCall that teaches the client the new call id.
/// </summary>
public sealed record ConferenceJoinOutcome(string? Error, IReadOnlyList<byte[]> Updates)
{
    public static ConferenceJoinOutcome Failed(string error) =>
        new(error, Array.Empty<byte[]>());

    public static ConferenceJoinOutcome Succeeded(IReadOnlyList<byte[]> updates) =>
        new(null, updates);
}
