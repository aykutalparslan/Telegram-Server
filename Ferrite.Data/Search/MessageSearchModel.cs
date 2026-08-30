// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Models;

namespace Ferrite.Data.Search;

public record MessageSearchModel(string Id, long UserId, int FromType, long FromId, 
    int PeerType, long PeerId, int MessageId, int? TopMessageId, string Message,
    int Date);