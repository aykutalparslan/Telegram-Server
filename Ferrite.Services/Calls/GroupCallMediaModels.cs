// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Calls;

/// <summary>
/// One DTLS fingerprint as it travels on the tgcalls group-call JSON contract.
/// The client offers <c>setup:"passive"</c> (it is the DTLS server); Ferrite's
/// connection answer carries the worker's fingerprints with <c>setup:"active"</c>.
/// </summary>
public sealed record GroupCallDtlsFingerprint(string Hash, string Value, string Setup);

/// <summary>
/// One ICE candidate advertised by the media worker. Every field is a string on
/// the wire, matching <c>GroupJoinPayloadInternal.cpp</c>.
/// </summary>
public sealed record GroupCallIceCandidate(
    string Port,
    string Protocol,
    string Network,
    string Generation,
    string Id,
    string Component,
    string Foundation,
    string Priority,
    string Ip,
    string Type);

/// <summary>One RTCP feedback entry. Subtype is absent for single-word forms.</summary>
public sealed record GroupCallRtcpFeedback(string Type, string? Subtype);

/// <summary>
/// One negotiable video codec offered to the client. <see cref="Id"/> is assigned
/// by the media worker, not by Ferrite: mediasoup allocates the dynamic payload
/// range itself, so this value is always read back from the worker.
/// </summary>
public sealed record GroupCallVideoPayloadType(
    int Id,
    string Name,
    int ClockRate,
    int Channels,
    IReadOnlyList<GroupCallRtcpFeedback> FeedbackTypes,
    IReadOnlyList<KeyValuePair<string, string>> Parameters);

/// <summary>One RTP header extension mapping.</summary>
public sealed record GroupCallRtpExtension(int Id, string Uri);

/// <summary>
/// The video half of the connection answer. <see cref="ServerSource"/> is the
/// worker's bandwidth probing SSRC. Null on an audio-only join.
/// </summary>
public sealed record GroupCallVideoAnswer(
    string Endpoint,
    int ServerSource,
    IReadOnlyList<GroupCallVideoPayloadType> PayloadTypes,
    IReadOnlyList<GroupCallRtpExtension> Extensions);

/// <summary>
/// The ICE/DTLS transport the media worker exposes for one participant. It is
/// serialized into <c>updateGroupCallConnection.params</c> for the joining
/// client and never persisted.
/// </summary>
public sealed record GroupCallMediaTransport(
    string Ufrag,
    string Pwd,
    IReadOnlyList<GroupCallDtlsFingerprint> Fingerprints,
    IReadOnlyList<GroupCallIceCandidate> Candidates,
    GroupCallVideoAnswer? Video = null);

/// <summary>
/// One tgcalls video source group. <c>SIM</c> lists simulcast layer SSRCs in
/// quality order; <c>FID</c> pairs a media SSRC with its RTX SSRC. Sources are
/// signed int32 wire values; compare them as uint32 against media SSRCs.
/// </summary>
public sealed record GroupCallVideoSourceGroup(
    string Semantics,
    IReadOnlyList<int> Sources);

/// <summary>
/// The validated tgcalls <c>joinGroupCall.params</c> payload. Source is the
/// signed int32 wire value; compare it as uint32 against media SSRCs.
/// <see cref="VideoSourceGroups"/> is empty on an audio-only join.
/// </summary>
public sealed record GroupCallJoinPayload(
    int Source,
    string Ufrag,
    string Pwd,
    IReadOnlyList<GroupCallDtlsFingerprint> Fingerprints,
    IReadOnlyList<GroupCallVideoSourceGroup> VideoSourceGroups);
