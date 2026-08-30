// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public interface IGroupCallsRepository
{
    ValueTask<GroupCallCreateResult> TryCreateCallAsync(TLDto.TLGroupCallState call,
        CancellationToken cancellationToken = default);
    ValueTask<GroupCallCreateResult> TryCreateConferenceCallAsync(
        TLDto.TLGroupCallState call, CancellationToken cancellationToken = default);
    ValueTask<TLDto.TLGroupCallState?> GetCallAsync(long callId,
        CancellationToken cancellationToken = default);
    ValueTask<TLDto.TLGroupCallState?> GetActiveCallByPeerAsync(int peerType, long peerId,
        CancellationToken cancellationToken = default);
    ValueTask<TLDto.TLGroupCallState?> GetCallByPeerRandomIdAsync(int peerType, long peerId,
        int randomId, CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<TLDto.TLGroupCallState>> GetActiveCallsAsync(
        CancellationToken cancellationToken = default);
    ValueTask<GroupCallMutationResult> TrySetJoinMutedAsync(long callId, bool joinMuted,
        CancellationToken cancellationToken = default);
    ValueTask<GroupCallMutationResult> TrySetTitleAsync(long callId, string title,
        CancellationToken cancellationToken = default);
    ValueTask<GroupCallMutationResult> TryStartScheduledAsync(long callId, int startedDate,
        CancellationToken cancellationToken = default);
    ValueTask<GroupCallViewerMutationResult> TrySetStartSubscriptionAsync(long callId,
        long userId, bool subscribed, CancellationToken cancellationToken = default);
    ValueTask<GroupCallMutationResult> TryRotateInviteGenerationAsync(long callId,
        CancellationToken cancellationToken = default);
    ValueTask<GroupCallMutationResult> TryAdvanceMediaEpochAsync(long callId,
        CancellationToken cancellationToken = default);
    ValueTask<GroupCallRecordingMutationResult> TryStartRecordingAsync(long callId,
        int startDate, long initiatingUserId, string title, bool video, bool portrait,
        CancellationToken cancellationToken = default);
    ValueTask<GroupCallRecordingMutationResult> TryStopRecordingAsync(long callId,
        int expectedGeneration, CancellationToken cancellationToken = default);
    ValueTask<GroupCallRecoveryResult> TryMarkTransportsStaleAsync(long callId,
        CancellationToken cancellationToken = default);
    ValueTask<GroupCallDiscardResult> TryDiscardCallAsync(long callId, int endedDate,
        int duration, int? expectedState = null,
        CancellationToken cancellationToken = default);

    ValueTask<GroupCallJoinResult> TryJoinParticipantAsync(
        TLDto.TLGroupCallParticipantState participant,
        CancellationToken cancellationToken = default);
    ValueTask<GroupCallLeaveResult> TryLeaveParticipantAsync(long callId, long userId,
        CancellationToken cancellationToken = default);
    ValueTask<GroupCallParticipantEditResult> TryEditParticipantAsync(long callId,
        long userId, GroupCallParticipantEditSpec edit,
        CancellationToken cancellationToken = default);
    ValueTask<GroupCallParticipantEditResult> TrySetParticipantPresentationAsync(
        long callId, long userId, string? presentationEndpoint,
        CancellationToken cancellationToken = default);
    ValueTask<bool> TryTouchParticipantActiveDateAsync(long callId, long userId,
        int activeDate, CancellationToken cancellationToken = default);

    ValueTask<int> CountActiveVideoParticipantsAsync(long callId,
        CancellationToken cancellationToken = default);
    ValueTask<TLDto.TLGroupCallParticipantState?> GetParticipantAsync(long callId,
        long userId, CancellationToken cancellationToken = default);
    ValueTask<TLDto.TLGroupCallParticipantState?> GetParticipantBySourceAsync(long callId,
        int source, CancellationToken cancellationToken = default);
    ValueTask<GroupCallParticipantPage> GetParticipantsPageAsync(long callId,
        string? offset, int limit, CancellationToken cancellationToken = default);

    ValueTask<bool> PutViewerStateAsync(TLDto.TLGroupCallViewerState state,
        CancellationToken cancellationToken = default);
    ValueTask<TLDto.TLGroupCallViewerState?> GetViewerStateAsync(long callId, long userId,
        CancellationToken cancellationToken = default);
    ValueTask<bool> PutViewerParticipantStateAsync(
        TLDto.TLGroupCallViewerParticipantState state,
        CancellationToken cancellationToken = default);
    ValueTask<TLDto.TLGroupCallViewerParticipantState?> GetViewerParticipantStateAsync(
        long callId, long viewerUserId, long targetUserId,
        CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<TLDto.TLGroupCallViewerParticipantState>>
        GetViewerParticipantStatesAsync(long callId, long viewerUserId,
        CancellationToken cancellationToken = default);

    ValueTask<bool> SaveDefaultJoinAsAsync(TLDto.TLGroupCallDefaultJoinAs joinAs,
        CancellationToken cancellationToken = default);
    ValueTask<TLDto.TLGroupCallDefaultJoinAs?> GetDefaultJoinAsAsync(long userId,
        int peerType, long peerId, CancellationToken cancellationToken = default);

    ValueTask<bool> PutInviteAsync(TLDto.TLGroupCallInvite invite,
        CancellationToken cancellationToken = default);
    ValueTask<TLDto.TLGroupCallInvite?> GetInviteByHashAsync(string hash,
        CancellationToken cancellationToken = default);
    ValueTask<IReadOnlyList<TLDto.TLGroupCallInvite>> GetInvitesByCallAsync(long callId,
        CancellationToken cancellationToken = default);
    ValueTask<bool> TryRevokeInviteAsync(long callId, string hash,
        CancellationToken cancellationToken = default);
}
