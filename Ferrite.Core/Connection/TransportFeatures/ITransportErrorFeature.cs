// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Buffers;

namespace Ferrite.Core.Connection.TransportFeatures;

public interface ITransportErrorFeature
{
    public ReadOnlySequence<byte> GenerateTransportError(int errorCode);
}