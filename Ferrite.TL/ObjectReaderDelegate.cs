// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.TL;

public delegate Span<byte> ObjectReaderDelegate(Span<byte> buffer, int offset);