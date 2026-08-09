// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public enum GroupCallPersistenceState
{
    Scheduled = 0,
    Active = 1,
    Discarded = 2,
}

// dto.groupCallState.peer_type / dto.groupCallParticipantState.peer_type. Group
// calls are hosted by a basic group or a channel only, so this is deliberately
// narrower than Ferrite.Data.PeerType and does not share its numbering.
// None marks an E2E conference call, which has no host peer at all; see the
// dto.groupCallState comment in baseLayer.tl for how a peerless row is indexed.
public enum GroupCallPeerType
{
    None = -1,
    Chat = 0,
    Channel = 1,
}

public enum GroupCallCreateStatus
{
    Created,
    Idempotent,
    ActiveCallExists,
    IdCollision,
}

public sealed record GroupCallCreateResult(GroupCallCreateStatus Status,
    TLDto.TLGroupCallState? Call);

public enum GroupCallMutationStatus
{
    Updated,
    NoChange,
    NotFound,
    InvalidState,
}

public sealed record GroupCallMutationResult(GroupCallMutationStatus Status,
    TLDto.TLGroupCallState? Call);

public enum GroupCallRecordingMutationStatus
{
    Started,
    Stopped,
    NoChange,
    NotFound,
    InvalidState,
    GenerationMismatch,
}

public sealed record GroupCallRecordingMutationResult(
    GroupCallRecordingMutationStatus Status, TLDto.TLGroupCallState? Call);

public enum GroupCallViewerMutationStatus
{
    Updated,
    NoChange,
    CallNotFound,
    CallNotScheduled,
}

public sealed record GroupCallViewerMutationResult(GroupCallViewerMutationStatus Status,
    TLDto.TLGroupCallState? Call);

public enum GroupCallRecoveryStatus
{
    Reconciled,
    NoStaleParticipants,
    CallNotFound,
    CallNotActive,
}

/// <summary>
/// Result of one startup/worker-restart transport reconciliation. Participant
/// rows are updated inside the repository lock and are not returned as owned TL
/// values because startup has no live request that could consume them.
/// </summary>
public sealed record GroupCallRecoveryResult(GroupCallRecoveryStatus Status,
    int StaleParticipants, int Version, int MediaEpoch);

public enum GroupCallDiscardStatus
{
    Discarded,
    AlreadyDiscarded,
    StateChanged,
    NotFound,
}

public sealed record GroupCallDiscardResult(GroupCallDiscardStatus Status,
    TLDto.TLGroupCallState? Call);

public enum GroupCallJoinStatus
{
    Joined,
    Rejoined,
    DuplicateSource,
    InvalidSource,
    CallNotFound,
    CallNotActive,
}

public sealed record GroupCallJoinResult(GroupCallJoinStatus Status,
    TLDto.TLGroupCallParticipantState? Participant, TLDto.TLGroupCallState? Call);

public enum GroupCallLeaveStatus
{
    Left,
    NotJoined,
    CallNotFound,
}

public sealed record GroupCallLeaveResult(GroupCallLeaveStatus Status,
    TLDto.TLGroupCallParticipantState? Participant, TLDto.TLGroupCallState? Call);

public enum GroupCallParticipantEditStatus
{
    Updated,
    NoChange,
    NotJoined,
    CallNotFound,
}

/// <summary>
/// One participant edit as nullable overrides: null leaves the stored value
/// untouched, a value replaces it. Flag fields accept false to CLEAR the stored
/// flag, which the generated builders cannot express through Clone(), so the row
/// is rebuilt field by field. <see cref="ClearRaiseHand"/> exists because
/// raise_hand_rating is an optional scalar whose absence — not zero — means the
/// hand is down.
/// </summary>
public sealed record GroupCallParticipantEditSpec
{
    public bool? Muted { get; init; }
    public bool? CanSelfUnmute { get; init; }
    public int? Volume { get; init; }
    public long? RaiseHandRating { get; init; }
    public bool ClearRaiseHand { get; init; }
    public bool? VideoStopped { get; init; }
    public bool? VideoPaused { get; init; }
    public bool? PresentationPaused { get; init; }
    public bool? VideoJoined { get; init; }
}

public sealed record GroupCallParticipantEditResult(GroupCallParticipantEditStatus Status,
    TLDto.TLGroupCallParticipantState? Participant, TLDto.TLGroupCallState? Call);

public sealed record GroupCallParticipantPage(
    IReadOnlyList<TLDto.TLGroupCallParticipantState> Participants,
    string? NextOffset);

/// <summary>
/// The only two tde2e sub-chains a conference call has. Sub-chain 0 carries the
/// validated blocks; sub-chain 1 carries the commit/reveal broadcasts, whose
/// semantics are enforced client-side. Anything else is a client error.
/// </summary>
public static class GroupCallSubChain
{
    public const int Blocks = 0;
    public const int Broadcast = 1;
}

/// <summary>
/// One tde2e append attempt. <c>Committed == false</c> means the expected height
/// lost a race, and <see cref="Height"/> is then the head that actually won so
/// the caller can rebuild against it instead of forking.
/// </summary>
public readonly record struct GroupCallChainAppendResult(bool Committed, int Height);
