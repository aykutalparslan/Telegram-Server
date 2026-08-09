// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Services.Calls;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.phone;
using Ferrite.Utils;
using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Phone.Handlers;

/// <summary>
/// phone.checkGroupCall. Answers which of the sources a client believes it is
/// receiving are still live: the intersection of the requested nonzero sources,
/// active participant rows, and current worker liveness — with a connect grace,
/// because a participant that has not finished ICE/DTLS yet has not been dropped.
///
/// This endpoint is strictly read-only. It never evicts anybody — a source that
/// fails the liveness check is simply omitted, and the disconnect grace path owns
/// the mutation that eventually marks that participant left.
/// </summary>
public sealed class CheckGroupCallHandler : GroupCallHandlerBase
{
    private readonly IGroupCallsRepository _groupCallsRepository;

    // The client only ever asks about sources it is actually consuming, so this
    // bounds a raw client rather than a real one.
    private const int MaxSources = 256;

    private readonly IGroupCallMediaPlane _media;
    private readonly GroupCallDisconnectOptions _disconnectOptions;

    public CheckGroupCallHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IAuthorizationRepository authorizationRepository, IGroupCallsRepository groupCallsRepository, UpdateFanout fanout,
        GroupCallChatLink chatLink, IUpdatesContextFactory updatesContexts,
        IMTProtoTime time, GroupCallVideoOptions videoOptions,
        GroupCallMediaSourceMap sourceMap, ILogger log, IGroupCallMediaPlane media,
        GroupCallDisconnectOptions disconnectOptions)
        : base(unitOfWork, chatParticipantsRepository, chatRepository, authorizationRepository, groupCallsRepository, fanout, chatLink, updatesContexts, time, videoOptions,
            sourceMap, log)
    {
        _groupCallsRepository = groupCallsRepository;

        _media = media;
        _disconnectOptions = disconnectOptions;
    }

    [TLFunction(Constructors.baseLayer_CheckGroupCall)]
    public async ValueTask<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var request = (CheckGroupCall)q;
        bool callRead = TryReadInputGroupCall(request.Get_CallView(), out long callId,
            out long accessHash);
        List<int> requested = ReadSources(request.Sources);

        if (!callRead)
        {
            return Error(GroupCallErrors.GroupCallInvalid);
        }

        using GroupCallResolution resolution = await ResolveCallAsync(authKeyId, callId,
            accessHash, GroupCallAccessLevel.Read);
        if (resolution.Error != null)
        {
            return Error(resolution.Error);
        }
        if (resolution.Call!.Value.AsGroupCallState().State !=
            (int)GroupCallPersistenceState.Active)
        {
            // A call that is not running has no live sources, which is an empty
            // answer rather than an error: the client is asking exactly so it can
            // discover that.
            return ToIntVector(Array.Empty<int>());
        }

        var alive = new List<int>(requested.Count);
        foreach (int source in requested)
        {
            if (await IsSourceAliveAsync(callId, source))
            {
                alive.Add(source);
            }
        }

        Log.Debug($"📞 checkGroupCall call:{callId} requested:{requested.Count} " +
                  $"alive:{alive.Count}");
        return ToIntVector(alive);
    }

    /// <summary>
    /// A source is live when it still owns an active participant row and either
    /// the worker holds a connected transport for it or it joined recently enough
    /// to still be connecting. A row that is gone, left, or re-sourced is never
    /// live, whatever the worker says.
    /// </summary>
    private async ValueTask<bool> IsSourceAliveAsync(long callId, int source)
    {
        using TLDto.TLGroupCallParticipantState? participant = await _groupCallsRepository.GetParticipantBySourceAsync(callId, source);
        if (participant == null)
        {
            return false;
        }

        var view = participant.Value.AsGroupCallParticipantState();
        if (view.Left || view.Source != source)
        {
            return false;
        }
        string mediaId = Encoding.UTF8.GetString(view.MediaId);
        int joinDate = view.JoinDate;

        try
        {
            if (await _media.IsAliveAsync(callId, mediaId))
            {
                return true;
            }
        }
        catch (GroupCallMediaException e)
        {
            // An unreachable worker is not evidence that this participant is gone,
            // but it is also not evidence that it is live. Reporting it as not
            // alive only makes the client re-check.
            Log.Warning(e, $"📞 checkGroupCall could not read liveness for " +
                           $"call:{callId} media:{mediaId}");
            return false;
        }

        // The worker has no connected transport for it. That is only evidence of
        // a drop once the participant has had time to connect: ICE and DTLS
        // complete after the join answer, and this call fires ten seconds after
        // every join. Answering "not alive" inside that window makes pinned TDLib
        // conclude it lost the call and leave. A transport that never arrives is
        // still evicted by GroupCallDisconnectMonitor.
        return Now() - joinDate < (int)_disconnectOptions.ConnectGrace.TotalSeconds;
    }

    /// <summary>
    /// Zero is never a valid SSRC and duplicates would only repeat an answer, so
    /// both are dropped before any lookup happens. Reading the vector is
    /// synchronous: VectorOfInt is a ref struct.
    /// </summary>
    private static List<int> ReadSources(VectorOfInt sources)
    {
        var result = new List<int>();
        var seen = new HashSet<int>();
        int count = Math.Min(sources.Count, MaxSources);
        for (int i = 0; i < count; i++)
        {
            int source = sources[i];
            if (source != 0 && seen.Add(source))
            {
                result.Add(source);
            }
        }

        return result;
    }

    private static TLBytes ToIntVector(IReadOnlyCollection<int> values)
    {
        var vector = new VectorOfInt();
        foreach (int value in values)
        {
            vector.Append(value);
        }

        byte[] bytes = vector.ToReadOnlySpan().ToArray();
        return new TLBytes(bytes, 0, bytes.Length);
    }

    private static TLBytes Error(string message) =>
        RpcErrorGenerator.GenerateError(400, Encoding.UTF8.GetBytes(message));
}
