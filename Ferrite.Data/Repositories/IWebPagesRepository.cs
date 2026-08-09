// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public interface IWebPagesRepository
{
    bool PutWebPage(TLWebPageInfo page);
    ValueTask<TLWebPageInfo?> GetWebPageAsync(string url);
}
