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

// Who a group-call row is being rendered for. Every flag a client can disagree
// about between two accounts is resolved from this, never from the stored row
// alone, so no viewer ever receives another viewer's perspective.
public sealed record GroupCallViewer(long UserId, bool CanManageCall,
    bool ScheduleStartSubscribed);

// The viewer-local overlay for one participant row: the viewer's own mute and
// volume for that participant, plus the per-viewer media sources. mediasoup
// rewrites SSRCs per consumer, so the source a viewer must see is its
// own mapping and not the canonical join-payload SSRC. Sources is null when the
// viewer has no media mapping for this participant, which omits the video rows
// entirely rather than publishing SSRCs the viewer cannot receive.
public readonly record struct GroupCallParticipantOverlay(bool MutedByYou,
    int? LocalVolume, int Source, GroupCallViewerSources? Sources = null)
{
    public static GroupCallParticipantOverlay None => default;
}

// Update-path decorations that are not part of the stored row. just_joined and
// versioned are set by the join/leave writers; min marks a row built without a
// viewer-local overlay, which tells the client to keep its local state.
[Flags]
public enum GroupCallParticipantDecoration
{
    None = 0,
    JustJoined = 1,
    Versioned = 2,
    Min = 4,
}

// A peer a built row refers to, so callers can hydrate the related users/chats
// vectors without re-reading the rows.
public readonly record struct GroupCallReferencedPeer(TLPeer.PeerType Type, long Id);

// The one place stored group-call rows become TL. Video rows are built PER
// VIEWER from the media plane's rewritten sources, never from the stored row
// alone. Broadcast viewing is advertised for every active non-conference call;
// the RTMP bit still comes from durable create state. Recording and conference
// flags come from the same durable call row.
public static class GroupCallBuilders
{
    // Ferrite pages participants by ascending join date, which is what
    // join_date_asc advertises to the client.
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
        // A conference has no hosting dialog, so this flag is the only thing that
        // tells the client the call is end-to-end encrypted and must be driven
        // through the chain rather than through a chat.
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
        // The client hides its camera button once the call is at capacity.
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
        // The builder stores these as spans and only copies them in Build(), so
        // both must stay alive until then. Disposing either one early returns its
        // pooled buffer, which the next rent hands straight back out — the camera
        // span then aliases the screen's memory and Build() writes a wrong length.
        //
        // Emission is gated on the STORED row, not the media mapping: the worker
        // keeps a camera/screen transport alive while the participant has merely
        // stopped or ended it, so the mapping alone would keep advertising a
        // stream nobody is sending. The paused bits likewise come from the
        // committed row rather than the mapping snapshot.
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

            // A min row carries no viewer-local state at all: the client keeps whatever
            // mute/volume it already holds for that participant.
            if (!isMin && !isSelf)
            {
                if (overlay.MutedByYou)
                {
                    builder = builder.MutedByYou(true);
                }
                // An admin-set volume overrides the viewer's local one and is flagged so
                // the client stops offering a local override.
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

    // Built per viewer: the source groups are the SSRCs the worker rewrote for
    // this consumer, never the producer's canonical values. Vectors are built
    // synchronously here because Vector is a ref struct.
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

    // The join answer. This update carries the caller's own media credentials, so
    // it is built directly into the join RPC result — first, before any call or
    // participant update — and is never enqueued to an update box or fanned out to
    // other members. UpdatesService hydrates no related objects for it.
    //
    // A participant may hold two live transports at once (camera and screen
    // share), and `presentation` is the only thing that tells the client which
    // credential set it just received.
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

    // One sub-chain's poll answer. next_offset is what the client stores as its
    // own next offset and TDLib applies the LAST (next_offset - its own) entries,
    // so the window must be contiguous and end exactly there. Both sub-chains are
    // always sent on a join, even when one of them is empty, because the client
    // waits for both before the call becomes encrypted.
    public static TLUpdate BuildChainBlocksUpdate(long callId, long accessHash,
        int subChainId, IReadOnlyList<byte[]> blocks, int nextOffset)
    {
        using TLInputGroupCall inputCall = BuildInputGroupCall(callId, accessHash);
        var vector = new VectorOfString();
        foreach (byte[] block in blocks)
        {
            // Served blocks leave in tde2e's server form; see
            // ChainBlockCodec.ToServerForm. This is the one place a stored block
            // becomes a wire block, so the conversion belongs here rather than in
            // each caller.
            vector.AppendTLBytes(ChainBlockCodec.ToServerForm(block));
        }

        return UpdateGroupCallChainBlocks.Builder()
            .Call(inputCall.AsSpan())
            .SubChainId(subChainId)
            .Blocks(vector)
            .NextOffset(nextOffset)
            .Build();
    }

    // Every peer a participant page refers to: the join-as peer plus the account
    // behind it, so a user who joined as a channel is still hydrated in `users`.
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

    // Self keeps the canonical join-payload SSRC; every other viewer sees the
    // source the media plane rewrote for it, falling back to the canonical value
    // while no media mapping exists yet (scheduled calls, left rows).
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
