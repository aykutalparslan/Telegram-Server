// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Services.Calls;
using Ferrite.TL;
using Ferrite.TL.baseLayer.phone;
using Ferrite.Utils;
using TLDto = Ferrite.TL.baseLayer.dto;
using TLUpdatesResult = Ferrite.TL.baseLayer.TLUpdates;

namespace Ferrite.Services.Phone.Handlers;

/// <summary>
/// phone.toggleGroupCallRecord. Recording is a call-only manager setting, so it
/// preserves the participant version and publishes viewer-correct
/// updateGroupCall rows. The coordinator owns the long-running worker/import
/// transition and makes repeated desired states GROUPCALL_NOT_MODIFIED, which
/// pinned TDLib maps to success.
/// </summary>
public sealed class ToggleGroupCallRecordHandler : GroupCallHandlerBase
{
    private const int MaxTitleLength = 64;
    private readonly IGroupCallRecordingCoordinator _recording;
    private readonly GroupCallRecordingOptions _recordingOptions;

    public ToggleGroupCallRecordHandler(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IAuthorizationRepository authorizationRepository, IGroupCallsRepository groupCallsRepository, UpdateFanout fanout,
        GroupCallChatLink chatLink, IUpdatesContextFactory updatesContexts,
        IMTProtoTime time, GroupCallVideoOptions videoOptions,
        GroupCallMediaSourceMap sourceMap, ILogger log,
        IGroupCallRecordingCoordinator recording,
        GroupCallRecordingOptions recordingOptions)
        : base(unitOfWork, chatParticipantsRepository, chatRepository, authorizationRepository, groupCallsRepository, fanout, chatLink, updatesContexts, time, videoOptions,
            sourceMap, log)
    {
        _recording = recording;
        _recordingOptions = recordingOptions;
    }

    [TLFunction(Constructors.baseLayer_ToggleGroupCallRecord)]
    public async ValueTask<TLUpdatesResult> Handle(long authKeyId, TLBytes q)
    {
        var request = (ToggleGroupCallRecord)q;
        bool callRead = TryReadInputGroupCall(request.Get_CallView(), out long callId,
            out long accessHash);
        bool start = request.Start;
        bool video = request.Video;
        bool portrait = request.Video && request.VideoPortrait;
        string title = request.Flags[1]
            ? Encoding.UTF8.GetString(request.Title).Trim()
            : string.Empty;

        if (!callRead)
        {
            return Error(GroupCallErrors.GroupCallInvalid);
        }
        if (title.Length > MaxTitleLength ||
            Encoding.UTF8.GetByteCount(title) > _recordingOptions.MaxTitleBytes)
        {
            return Error(GroupCallErrors.TitleInvalid);
        }

        using GroupCallResolution resolution = await ResolveCallAsync(authKeyId, callId,
            accessHash, GroupCallAccessLevel.Manage);
        if (resolution.Error != null)
        {
            return Error(resolution.Error);
        }

        GroupCallPeerAccess access = resolution.Access!;
        GroupCallRecordingTransitionResult transition = await _recording.ToggleAsync(
            callId, start, access.CurrentUserId, title, video, portrait, Now());
        if (transition.Status == GroupCallRecordingTransitionStatus.NoChange)
        {
            transition.Call?.Dispose();
            return Error(GroupCallErrors.GroupCallNotModified);
        }
        if (transition.Status == GroupCallRecordingTransitionStatus.MediaUnavailable)
        {
            transition.Call?.Dispose();
            return Error(GroupCallErrors.MediaUnavailable);
        }
        if (transition.Status is not (GroupCallRecordingTransitionStatus.Started or
            GroupCallRecordingTransitionStatus.Stopped) || transition.Call == null)
        {
            transition.Call?.Dispose();
            return Error(GroupCallErrors.GroupCallInvalid);
        }

        using TLDto.TLGroupCallState updated = transition.Call.Value;
        int videoCount = await CountUnmutedVideoAsync(callId);
        GroupCallViewer viewer = await BuildViewerAsync(callId, access.CurrentUserId,
            access.CanManageCall);
        var updates = new List<byte[]>(1)
        {
            BuildCallUpdateBytes(updated, viewer, access.Peer.Id, videoCount)
        };
        await PushCallUpdateToOtherMembersAsync(updated, access.Peer.Id,
            access.CurrentUserId, videoCount);

        Log.Debug($"📼 toggleGroupCallRecord call:{callId} " +
                  $"by:{access.CurrentUserId} start:{start} video:{video} " +
                  $"portrait:{portrait}");
        return await BuildInvokerResultAsync(authKeyId, access.CurrentUserId, updates,
            access.ChatBytes!);
    }

    private static TLUpdatesResult Error(string message) =>
        (TLUpdatesResult)RpcErrorGenerator.GenerateError(400,
            Encoding.UTF8.GetBytes(message));
}
