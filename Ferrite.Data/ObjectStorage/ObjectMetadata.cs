// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Org.BouncyCastle.Asn1.X9;

namespace Ferrite.Data.ObjectStorage;

public record struct ObjectMetadata(long FileId, int PartNum, int Size, DateTimeOffset Timestamp, bool IsBig);