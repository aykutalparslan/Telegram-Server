// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Buffers;
using Ferrite.Services;

namespace Ferrite.Core.Connection.TransportFeatures;

public interface IQuickAckFeature
{
    public ReadOnlySequence<byte> GenerateQuickAck(int ack, MTProtoTransport transport);
}