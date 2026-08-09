// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Buffers;

namespace Ferrite.Core.Connection.TransportFeatures;

public readonly record struct HandshakeResponse(SequencePosition Position, 
    ReadOnlySequence<byte> Response, bool Completed);