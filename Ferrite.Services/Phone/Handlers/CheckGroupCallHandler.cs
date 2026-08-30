// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.Services.Calls;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.phone;
using Ferrite.Utils;
using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Phone.Handlers;

public sealed class CheckGroupCallHandler : GroupCallHandlerBase
{
    private readonly IGroupCallsRepository _groupCallsRepository;

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
            Log.Warning(e, $"📞 checkGroupCall could not read liveness for " +
                           $"call:{callId} media:{mediaId}");
            return false;
        }

        return Now() - joinDate < (int)_disconnectOptions.ConnectGrace.TotalSeconds;
    }

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
