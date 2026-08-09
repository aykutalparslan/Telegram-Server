// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Data;

// Ferrite is a single-DC server: help.getConfig and help.getNearestDc
// advertise this_dc = 1, and the patched TDLib test build maps DC 1 to the
// local test endpoint. Every stored media row (photo#/document#/
// userProfilePhoto/chatPhoto) must advertise this same DC id, or clients
// would attempt a cross-DC auth.exportAuthorization flow that Ferrite does
// not implement.
public static class MediaDefaults
{
    public const int DcId = 1;
}
