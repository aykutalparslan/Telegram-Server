// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.GroupCallMedia;
using Ferrite.Services.Calls;

namespace Ferrite;

public sealed record FerriteServerOptions
{
    public required string PublicAddress { get; init; }

    public required int Port { get; init; }

    public string DataPath { get; init; } = "data";

    public Guid? NodeId { get; init; }

    public StorageOptions Storage { get; init; } = new();

    public CallMediaRelayOptions? CallMedia { get; init; }

    public CallTurnOptions? CallTurn { get; init; }

    public GroupCallMediaWorkerOptions? GroupCallMediaWorker { get; init; }

    public GroupCallVideoOptions? GroupCallVideo { get; init; }

    public GroupCallMediaRuntimeOptions? GroupCallMediaRuntime { get; init; }

    public GroupCallBroadcastOptions? GroupCallBroadcast { get; init; }

    public GroupCallRecordingOptions? GroupCallRecording { get; init; }
}
