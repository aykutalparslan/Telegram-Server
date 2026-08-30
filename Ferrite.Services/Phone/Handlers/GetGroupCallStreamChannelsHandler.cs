// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.Services.Calls;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.phone;
using Ferrite.Utils;

namespace Ferrite.Services.Phone.Handlers;

public sealed class GetGroupCallStreamChannelsHandler : GroupCallHandlerBase
{
    private readonly IGroupCallBroadcastPlane _broadcast;

    public GetGroupCallStreamChannelsHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IAuthorizationRepository authorizationRepository, IGroupCallsRepository groupCallsRepository,
        UpdateFanout fanout, GroupCallChatLink chatLink,
        IUpdatesContextFactory updatesContexts, IMTProtoTime time,
        GroupCallVideoOptions videoOptions, GroupCallMediaSourceMap sourceMap,
        ILogger log, IGroupCallBroadcastPlane broadcast)
        : base(unitOfWork, chatParticipantsRepository, chatRepository, authorizationRepository, groupCallsRepository, fanout, chatLink, updatesContexts, time, videoOptions,
            sourceMap, log)
    {
        _broadcast = broadcast;
    }

    [TLFunction(Constructors.baseLayer_GetGroupCallStreamChannels)]
    public async ValueTask<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        var request = (GetGroupCallStreamChannels)q;
        bool callRead = TryReadInputGroupCall(request.Get_CallView(), out long callId,
            out long accessHash);
        if (!callRead)
        {
            return Error(GroupCallErrors.GroupCallInvalid);
        }

        using GroupCallResolution resolution = await ResolveCallAsync(authKeyId,
            callId, accessHash, GroupCallAccessLevel.Read);
        if (resolution.Error != null)
        {
            return Error(resolution.Error);
        }
        if (resolution.Call!.Value.AsGroupCallState().State !=
            (int)GroupCallPersistenceState.Active)
        {
            return Error(GroupCallErrors.GroupCallInvalid);
        }
        if (await GetMediaIdAsync(callId,
                resolution.Access!.CurrentUserId) == null)
        {
            return Error(GroupCallErrors.GroupCallJoinMissing);
        }

        try
        {
            IReadOnlyList<GroupCallBroadcastChannel> available = await _broadcast
                .GetChannelsAsync(callId);
            var channels = new Vector();
            foreach (GroupCallBroadcastChannel channel in available)
            {
                using TLGroupCallStreamChannel value = GroupCallStreamChannel.Builder()
                    .Channel(channel.Channel)
                    .Scale(channel.Scale)
                    .LastTimestampMs(channel.LastTimestampMs)
                    .Build();
                channels.AppendTLObject(value.AsSpan());
            }
            var result = GroupCallStreamChannels.Builder()
                .Channels(channels)
                .Build();
            return result.TLBytes!.Value;
        }
        catch (GroupCallBroadcastException e)
        {
            Log.Warning(e, $"📡 getGroupCallStreamChannels failed for call:{callId}");
            return Error(MapError(e));
        }
    }

    private static string MapError(GroupCallBroadcastException error) =>
        error.Kind == GroupCallBroadcastFailureKind.Rejected
            ? GroupCallErrors.GroupCallInvalid
            : GroupCallErrors.MediaUnavailable;

    private static TLBytes Error(string message) =>
        RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));
}
