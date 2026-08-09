// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public sealed class WebPagesRepository : IWebPagesRepository
{
    private readonly IKVStore _pages;

    public WebPagesRepository(IKVStore pages)
    {
        _pages = pages;
        pages.SetSchema(new TableDefinition("ferrite", "web_pages",
            new KeyDefinition("pk",
                new DataColumn { Name = "url", Type = DataType.String })));
    }

    public bool PutWebPage(TLWebPageInfo page)
    {
        var info = page.AsWebPageInfo();
        return _pages.Put(page.AsSpan().ToArray(), Encoding.UTF8.GetString(info.Url));
    }

    public async ValueTask<TLWebPageInfo?> GetWebPageAsync(string url)
    {
        byte[]? bytes = await _pages.GetAsync(url);
        return bytes is { Length: > 0 }
            ? new TLWebPageInfo(bytes, 0, bytes.Length)
            : null;
    }
}
