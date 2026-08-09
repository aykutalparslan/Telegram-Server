// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Data.Repositories;

public struct DataColumn
{
    public string Name { get; init; }
    public DataType Type { get; init; }
}