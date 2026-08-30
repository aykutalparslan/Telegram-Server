// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using TLDto = Ferrite.TL.baseLayer.dto;
using Ferrite.Data.Models;

namespace Ferrite.Data.Repositories;

public enum GroupCallPersistenceState
{
    Scheduled = 0,
    Active = 1,
    Discarded = 2,
}

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

public static class GroupCallSubChain
{
    public const int Blocks = 0;
    public const int Broadcast = 1;
}

public readonly record struct GroupCallChainAppendResult(bool Committed, int Height);
