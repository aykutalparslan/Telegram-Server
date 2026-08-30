// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Data.Search;

public record UserSearchModel(long Id, string? Username, string? FirstName, string? LastName, string Phone);