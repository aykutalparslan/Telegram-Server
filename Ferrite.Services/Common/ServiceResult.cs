// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Diagnostics.CodeAnalysis;

namespace Ferrite.Services.Common;

public readonly record struct ServiceResult<T>(T? Result, bool Success, ErrorMessage ErrorMessage);