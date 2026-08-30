// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

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

public sealed class CreateConferenceCallHandler : ConferenceCallHandlerBase
{
    private readonly IGroupCallsRepository _groupCallsRepository;

    private readonly IdAllocators _ids;
    private readonly IGroupCallMediaPlane _media;
    private readonly ConferenceJoinOperation _join;

    public CreateConferenceCallHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IAuthorizationRepository authorizationRepository, IGroupCallsRepository groupCallsRepository, IMessageRepository messageRepository, UpdateFanout fanout,
        GroupCallChatLink chatLink, IUpdatesContextFactory updatesContexts,
        IMTProtoTime time, GroupCallVideoOptions videoOptions,
        GroupCallMediaSourceMap sourceMap, ILogger log, IGroupCallChainService chain,
        IdAllocators ids, IGroupCallMediaPlane media, ConferenceJoinOperation join)
        : base(unitOfWork, chatParticipantsRepository, chatRepository, authorizationRepository, groupCallsRepository, messageRepository, fanout, chatLink, updatesContexts, time, videoOptions,
            sourceMap, log, chain)
    {
        _groupCallsRepository = groupCallsRepository;

        _ids = ids;
        _media = media;
        _join = join;
    }

    [TLFunction(Constructors.baseLayer_CreateConferenceCall)]
    public async ValueTask<TLUpdatesResult> Handle(long authKeyId, TLBytes q)
    {
        var request = (CreateConferenceCall)q;
        bool join = request.Join;
        bool muted = request.Muted;
        bool videoStopped = request.VideoStopped;
        int randomId = request.RandomId;
        byte[] publicKey = join ? request.PublicKey.ToArray() : Array.Empty<byte>();
        byte[] block = join ? request.Block.ToArray() : Array.Empty<byte>();
        byte[] paramsJson = join
            ? ReadParamsJson(request.Get_ParamsPropertyView())
            : Array.Empty<byte>();

        long userId = await ResolveUserIdAsync(authKeyId);
        if (userId == 0)
        {
            return Error(GroupCallErrors.AuthKeyInvalid);
        }

        long callId = await _ids.NextGroupCallIdAsync();
        long accessHash;
        do
        {
            accessHash = Random.Shared.NextInt64();
        } while (accessHash == 0);

        try
        {
            await _media.CreateRoomAsync(callId);
        }
        catch (GroupCallMediaException e)
        {
            Log.Warning(e, $"📞 createConferenceCall could not allocate a room for " +
                           $"call:{callId} creator:{userId}");
            return Error(GroupCallErrors.MediaUnavailable);
        }

        int now = Now();
        GroupCallCreateResult created;
        using (TLDto.TLGroupCallState row = BuildConferenceRow(callId, accessHash, userId,
                   randomId, now))
        {
            created = await _groupCallsRepository
                .TryCreateConferenceCallAsync(row);
        }

        switch (created.Status)
        {
            case GroupCallCreateStatus.Created:
                break;
            case GroupCallCreateStatus.Idempotent:
                await ReleaseRoomAsync(callId);
                using (TLDto.TLGroupCallState existing = created.Call!.Value)
                {
                    return await BuildReplayResultAsync(authKeyId, userId, existing);
                }
            default:
                await ReleaseRoomAsync(callId);
                return Error(GroupCallErrors.GroupCallInvalid);
        }

        using TLDto.TLGroupCallState call = created.Call!.Value;
        if (!join)
        {
            GroupCallViewer creatorViewer = await BuildViewerAsync(callId, userId,
                canManageCall: true);
            byte[] callOnly = BuildConferenceCallUpdateBytes(call, creatorViewer,
                unmutedVideoCount: 0);
            Log.Debug($"📞 createConferenceCall call:{callId} creator:{userId} " +
                      $"random:{randomId} join:false");
            return await BuildConferenceResultAsync(authKeyId, userId,
                new[] { callOnly });
        }

        ConferenceJoinOutcome outcome = await _join.JoinResolvedForResultAsync(call,
            userId, isCreator: true, accessHash, publicKey, block, paramsJson, muted,
            videoStopped);
        if (outcome.Error != null)
        {
            await DiscardUnjoinableAsync(callId, now);
            return Error(outcome.Error);
        }

        Log.Debug($"📞 createConferenceCall call:{callId} creator:{userId} " +
                  $"random:{randomId} join:true");
        return BuildUnsequencedConferenceResult(userId, outcome.Updates);
    }

    private static byte[] ReadParamsJson(DataJSONView view) =>
        view.Is(out DataJSON json) ? json.Data.ToArray() : Array.Empty<byte>();

    private static TLDto.TLGroupCallState BuildConferenceRow(long callId, long accessHash,
        long creatorUserId, int randomId, int now) =>
        TLDto.GroupCallState.Builder()
            .Id(callId)
            .AccessHash(accessHash)
            .PeerType((int)GroupCallPeerType.None)
            .PeerId(creatorUserId)
            .CreatorUserId(creatorUserId)
            .RandomId(randomId)
            .State((int)GroupCallPersistenceState.Active)
            .CreatedDate(now)
            .StartedDate(now)
            .Version(1)
            .ParticipantsCount(0)
            .InviteGeneration(1)
            .MediaEpoch(1)
            .Conference(true)
            .Build();

    private async Task ReleaseRoomAsync(long callId)
    {
        try
        {
            await _media.EndRoomAsync(callId);
        }
        catch (GroupCallMediaException e)
        {
            Log.Warning(e, $"📞 createConferenceCall could not release the unused " +
                           $"room for call:{callId}");
        }
    }

    private async Task DiscardUnjoinableAsync(long callId, int now)
    {
        GroupCallDiscardResult discarded = await _groupCallsRepository
            .TryDiscardCallAsync(callId, now, duration: 0);
        discarded.Call?.Dispose();
        await UnitOfWork.SaveAsync();
        await ReleaseRoomAsync(callId);
    }

    private async ValueTask<TLUpdatesResult> BuildReplayResultAsync(long authKeyId,
        long userId, TLDto.TLGroupCallState call)
    {
        var view = call.AsGroupCallState();
        long callId = view.Id;
        bool isCreator = view.CreatorUserId == userId;
        GroupCallViewer viewer = await BuildViewerAsync(callId, userId, isCreator);
        int videoCount = await CountUnmutedVideoAsync(callId);
        return await BuildConferenceResultAsync(authKeyId, userId,
            new[] { BuildConferenceCallUpdateBytes(call, viewer, videoCount) });
    }
}
