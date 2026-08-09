// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL.baseLayer;
using Ferrite.Utils;
using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Calls;

// The parts of a join that are identical for a hosted call and an E2E
// conference: the stored participant row, what the joiner sees of its own
// camera, and how a media or commit failure maps to the wire. Only the
// authorization and the chain differ between the two, so only those live in the
// handlers.
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

    /// <summary>
    /// What the joiner itself sees of its own camera: the endpoint the worker
    /// assigned, against the source groups the client just advertised. The
    /// per-consumer mapping never contains a viewer's own producer, so this is
    /// the only place the self row can come from.
    /// </summary>
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

    /// <summary>
    /// Compensation for a join whose signaling half did not commit. It is
    /// best-effort: the participant row is not stored, so a worker that cannot be
    /// reached leaves an orphan transport the room teardown will collect rather
    /// than an error the client would act on.
    /// </summary>
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
