// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL.baseLayer;
using Ferrite.Utils;
using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Calls;

public static class GroupCallJoinRows
{
    public static TLDto.TLGroupCallParticipantState BuildParticipantRow(long callId,
        long userId, string mediaId, int source, int joinDate, bool muted,
        bool canSelfUnmute, bool videoJoined, bool videoStopped, string? videoEndpoint)
    {
        var builder = TLDto.GroupCallParticipantState.Builder()
            .CallId(callId)
            .UserId(userId)
            .PeerType((int)TLPeer.PeerType.PeerUser)
            .PeerId(userId)
            .JoinDate(joinDate)
            .Source(source)
            .MediaId(Encoding.UTF8.GetBytes(mediaId));
        if (muted)
        {
            builder = builder.Muted(true);
        }
        if (canSelfUnmute)
        {
            builder = builder.CanSelfUnmute(true);
        }
        if (videoStopped)
        {
            builder = builder.VideoStopped(true);
        }
        if (videoJoined)
        {
            builder = builder.VideoJoined(true);
        }
        if (videoEndpoint != null)
        {
            builder = builder.VideoEndpoint(Encoding.UTF8.GetBytes(videoEndpoint));
        }

        return builder.Build();
    }

    public static GroupCallViewerSources BuildSelfSources(GroupCallMediaJoinResult joined,
        GroupCallJoinPayload payload, bool videoJoined)
    {
        if (!videoJoined || joined.Transport.Video is not { } video)
        {
            return new GroupCallViewerSources(joined.CanonicalSource, null, null);
        }

        return new GroupCallViewerSources(joined.CanonicalSource,
            new GroupCallParticipantVideoSources(video.Endpoint, payload.VideoSourceGroups,
                joined.CanonicalSource, Paused: false),
            Presentation: null);
    }

    public static async Task ReleaseTransportAsync(IGroupCallMediaPlane media, ILogger log,
        long callId, string mediaId, string reason)
    {
        try
        {
            await media.LeaveAsync(callId, mediaId);
        }
        catch (GroupCallMediaException e)
        {
            log.Warning(e, $"📞 could not release the {reason} for " +
                           $"call:{callId} media:{mediaId}");
        }
    }

    public static string TranslateMediaFailure(GroupCallMediaFailureKind kind) => kind switch
    {
        GroupCallMediaFailureKind.Conflict => GroupCallErrors.SsrcDuplicateMuch,
        GroupCallMediaFailureKind.Rejected => GroupCallErrors.DataJsonInvalid,
        _ => GroupCallErrors.MediaUnavailable,
    };

    public static string TranslateJoinFailure(GroupCallJoinStatus status) => status switch
    {
        GroupCallJoinStatus.DuplicateSource => GroupCallErrors.SsrcDuplicateMuch,
        GroupCallJoinStatus.InvalidSource => GroupCallErrors.DataJsonInvalid,
        _ => GroupCallErrors.GroupCallInvalid,
    };
}
