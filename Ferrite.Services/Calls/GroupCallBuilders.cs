// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Services.Calls.E2E;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Calls;

public sealed record GroupCallViewer(long UserId, bool CanManageCall,
    bool ScheduleStartSubscribed);

public readonly record struct GroupCallParticipantOverlay(bool MutedByYou,
    int? LocalVolume, int Source, GroupCallViewerSources? Sources = null)
{
    public static GroupCallParticipantOverlay None => default;
}

[Flags]
public enum GroupCallParticipantDecoration
{
    None = 0,
    JustJoined = 1,
    Versioned = 2,
    Min = 4,
}

public readonly record struct GroupCallReferencedPeer(TLPeer.PeerType Type, long Id);

public static class GroupCallBuilders
{
    private const bool JoinDateAscending = true;

    public static TLGroupCall BuildCall(TLDto.TLGroupCallState state,
        GroupCallViewer viewer, GroupCallVideoOptions videoOptions,
        int unmutedVideoCount)
    {
        var call = state.AsGroupCallState();
        if (call.State == (int)GroupCallPersistenceState.Discarded)
        {
            return GroupCallDiscarded.Builder()
                .Id(call.Id)
                .AccessHash(call.AccessHash)
                .Duration(call.Duration)
                .Build();
        }

        var builder = GroupCall.Builder()
            .Id(call.Id)
            .AccessHash(call.AccessHash)
            .ParticipantsCount(call.ParticipantsCount)
            .UnmutedVideoLimit(videoOptions.UnmutedVideoLimit)
            .UnmutedVideoCount(unmutedVideoCount)
            .Version(call.Version)
            .JoinDateAsc(JoinDateAscending)
            .StreamDcId(MediaDefaults.DcId);
        if (call.RtmpStream)
        {
            builder = builder.RtmpStream(true);
        }
        if (call.Conference)
        {
            builder = builder.Conference(true);
        }
        if (call.RecordVideoActive)
        {
            builder = builder.RecordVideoActive(true);
        }
        if (call.Flags[9])
        {
            builder = builder.RecordStartDate(call.RecordStartDate);
        }
        if (call.JoinMuted)
        {
            builder = builder.JoinMuted(true);
        }
        if (viewer.CanManageCall)
        {
            builder = builder.CanChangeJoinMuted(true);
        }
        if (call.CreatorUserId == viewer.UserId)
        {
            builder = builder.Creator(true);
        }
        if (call.Flags[1])
        {
            builder = builder.Title(call.Title);
        }
        if (call.Flags[2])
        {
            builder = builder.ScheduleDate(call.ScheduleDate);
            if (viewer.ScheduleStartSubscribed)
            {
                builder = builder.ScheduleStartSubscribed(true);
            }
        }
        if (unmutedVideoCount < videoOptions.UnmutedVideoLimit)
        {
            builder = builder.CanStartVideo(true);
        }

        return builder.Build();
    }

    public static TLGroupCallParticipant BuildParticipant(
        TLDto.TLGroupCallParticipantState state, GroupCallViewer viewer,
        GroupCallParticipantOverlay overlay,
        GroupCallParticipantDecoration decoration = GroupCallParticipantDecoration.None)
    {
        var participant = state.AsGroupCallParticipantState();
        bool isSelf = participant.UserId == viewer.UserId;
        bool isMin = decoration.HasFlag(GroupCallParticipantDecoration.Min);
        using TLPeer peer = PeerResolver.BuildPeer((TLPeer.PeerType)participant.PeerType,
            participant.PeerId);

        var builder = GroupCallParticipant.Builder()
            .Peer(peer.AsSpan())
            .Date(participant.JoinDate)
            .Source(ResolveSource(participant, isSelf, overlay));
        if (participant.Muted)
        {
            builder = builder.Muted(true);
        }
        if (participant.Left)
        {
            builder = builder.Left(true);
        }
        if (participant.CanSelfUnmute)
        {
            builder = builder.CanSelfUnmute(true);
        }
        if (isSelf)
        {
            builder = builder.Self(true);
        }
        if (decoration.HasFlag(GroupCallParticipantDecoration.JustJoined))
        {
            builder = builder.JustJoined(true);
        }
        if (decoration.HasFlag(GroupCallParticipantDecoration.Versioned))
        {
            builder = builder.Versioned(true);
        }
        if (isMin)
        {
            builder = builder.Min(true);
        }
        if (participant.Flags[3])
        {
            builder = builder.ActiveDate(participant.ActiveDate);
        }
        if (participant.Flags[6])
        {
            builder = builder.About(participant.About);
        }
        if (participant.Flags[5])
        {
            builder = builder.RaiseHandRating(participant.RaiseHandRating);
        }

        if (participant.VideoJoined)
        {
            builder = builder.VideoJoined(true);
        }
        TLGroupCallParticipantVideo? camera = null;
        TLGroupCallParticipantVideo? screen = null;
        try
        {
            if (participant.VideoJoined && overlay.Sources?.Video is { } cameraSources)
            {
                camera = BuildVideo(cameraSources with { Paused = participant.VideoPaused });
                builder = builder.Video(camera.Value.AsSpan());
            }
            if (participant.Flags[12] &&
                overlay.Sources?.Presentation is { } screenSources)
            {
                screen = BuildVideo(screenSources with
                {
                    Paused = participant.PresentationPaused
                });
                builder = builder.Presentation(screen.Value.AsSpan());
            }

            if (!isMin && !isSelf)
            {
                if (overlay.MutedByYou)
                {
                    builder = builder.MutedByYou(true);
                }
                if (participant.Flags[4])
                {
                    builder = builder.Volume(participant.Volume).VolumeByAdmin(true);
                }
                else if (overlay.LocalVolume is { } localVolume)
                {
                    builder = builder.Volume(localVolume);
                }
            }
            else if (!isMin && participant.Flags[4])
            {
                builder = builder.Volume(participant.Volume);
            }

            return builder.Build();
        }
        finally
        {
            camera?.Dispose();
            screen?.Dispose();
        }
    }

    private static TLGroupCallParticipantVideo BuildVideo(
        GroupCallParticipantVideoSources sources)
    {
        var groups = new Vector();
        foreach (var group in sources.SourceGroups)
        {
            var ssrcs = new VectorOfInt();
            foreach (int source in group.Sources)
            {
                ssrcs.Append(source);
            }

            using TLGroupCallParticipantVideoSourceGroup built =
                GroupCallParticipantVideoSourceGroup.Builder()
                    .Semantics(Encoding.UTF8.GetBytes(group.Semantics))
                    .Sources(ssrcs)
                    .Build();
            groups.AppendTLObject(built.AsSpan());
        }

        var builder = GroupCallParticipantVideo.Builder()
            .Endpoint(Encoding.UTF8.GetBytes(sources.Endpoint))
            .SourceGroups(groups)
            .AudioSource(sources.AudioSource);
        if (sources.Paused)
        {
            builder = builder.Paused(true);
        }

        return builder.Build();
    }

    public static TLInputGroupCall BuildInputGroupCall(TLDto.TLGroupCallState state)
    {
        var call = state.AsGroupCallState();
        return BuildInputGroupCall(call.Id, call.AccessHash);
    }

    public static TLInputGroupCall BuildInputGroupCall(long callId, long accessHash) =>
        InputGroupCall.Builder().Id(callId).AccessHash(accessHash).Build();

    public static TLUpdate BuildConnectionUpdate(ReadOnlySpan<byte> paramsJson,
        bool presentation = false)
    {
        using TLDataJSON data = DataJSON.Builder().Data(paramsJson).Build();
        var builder = UpdateGroupCallConnection.Builder()
            .ParamsProperty(data.AsSpan());
        if (presentation)
        {
            builder = builder.Presentation(true);
        }

        return builder.Build();
    }

    public static TLUpdate BuildChainBlocksUpdate(long callId, long accessHash,
        int subChainId, IReadOnlyList<byte[]> blocks, int nextOffset)
    {
        using TLInputGroupCall inputCall = BuildInputGroupCall(callId, accessHash);
        var vector = new VectorOfString();
        foreach (byte[] block in blocks)
        {
            vector.AppendTLBytes(ChainBlockCodec.ToServerForm(block));
        }

        return UpdateGroupCallChainBlocks.Builder()
            .Call(inputCall.AsSpan())
            .SubChainId(subChainId)
            .Blocks(vector)
            .NextOffset(nextOffset)
            .Build();
    }

    public static IReadOnlyList<GroupCallReferencedPeer> ReferencedPeers(
        IReadOnlyList<TLDto.TLGroupCallParticipantState> participants)
    {
        var peers = new List<GroupCallReferencedPeer>();
        var seen = new HashSet<GroupCallReferencedPeer>();
        foreach (var state in participants)
        {
            var participant = state.AsGroupCallParticipantState();
            Add(new GroupCallReferencedPeer(TLPeer.PeerType.PeerUser, participant.UserId));
            Add(new GroupCallReferencedPeer((TLPeer.PeerType)participant.PeerType,
                participant.PeerId));
        }

        return peers;

        void Add(GroupCallReferencedPeer peer)
        {
            if (peer.Id > 0 && seen.Add(peer))
            {
                peers.Add(peer);
            }
        }
    }

    private static int ResolveSource(TLDto.GroupCallParticipantState participant,
        bool isSelf, GroupCallParticipantOverlay overlay)
    {
        if (isSelf || overlay.Source == 0)
        {
            return participant.Source;
        }

        return overlay.Source;
    }
}
