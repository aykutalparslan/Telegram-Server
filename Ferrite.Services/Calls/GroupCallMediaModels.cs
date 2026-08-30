// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Calls;

public sealed record GroupCallDtlsFingerprint(string Hash, string Value, string Setup);

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

public sealed record GroupCallRtcpFeedback(string Type, string? Subtype);

public sealed record GroupCallVideoPayloadType(
    int Id,
    string Name,
    int ClockRate,
    int Channels,
    IReadOnlyList<GroupCallRtcpFeedback> FeedbackTypes,
    IReadOnlyList<KeyValuePair<string, string>> Parameters);

public sealed record GroupCallRtpExtension(int Id, string Uri);

public sealed record GroupCallVideoAnswer(
    string Endpoint,
    int ServerSource,
    IReadOnlyList<GroupCallVideoPayloadType> PayloadTypes,
    IReadOnlyList<GroupCallRtpExtension> Extensions);

public sealed record GroupCallMediaTransport(
    string Ufrag,
    string Pwd,
    IReadOnlyList<GroupCallDtlsFingerprint> Fingerprints,
    IReadOnlyList<GroupCallIceCandidate> Candidates,
    GroupCallVideoAnswer? Video = null);

public sealed record GroupCallVideoSourceGroup(
    string Semantics,
    IReadOnlyList<int> Sources);

public sealed record GroupCallJoinPayload(
    int Source,
    string Ufrag,
    string Pwd,
    IReadOnlyList<GroupCallDtlsFingerprint> Fingerprints,
    IReadOnlyList<GroupCallVideoSourceGroup> VideoSourceGroups);
