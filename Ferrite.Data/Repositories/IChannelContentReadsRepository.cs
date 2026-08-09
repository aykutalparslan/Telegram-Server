// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

/// <summary>
/// Per-viewer content-read markers for the shared channel message box. A channel
/// post exists once, so clearing its mention/media-unread state must never mutate
/// the stored message; it is recorded here against the reading user instead.
/// </summary>
public interface IChannelContentReadsRepository
{
    bool PutContentRead(TLChannelContentRead read);
    ValueTask<TLChannelContentRead?> GetContentReadAsync(long userId,
        long channelId, int messageId);
}
