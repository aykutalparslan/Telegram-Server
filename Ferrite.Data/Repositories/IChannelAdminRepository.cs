// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

/// <summary>
/// Durable channel administration state: the `channelFull` administration fields
/// that the compact `channel` row has no place for, plus the per-user slow-mode
/// deadline.
///
/// The split is deliberate. `channel#fe685355` already carries `signatures`,
/// `signature_profiles`, `autotranslation`, `join_to_send`, `join_request`,
/// `gigagroup`, `slowmode_enabled`, `has_geo`, `has_link`, `usernames`, `color`,
/// `profile_color` and `emoji_status`, and that stored row is what every reader
/// is served, so it stays authoritative for them rather than being shadowed by a
/// second copy here. This row holds only what has no home there:
/// `antispam`, `participants_hidden`, `hidden_prehistory`, `can_view_stats`,
/// `stats_dc`, `location`, `linked_chat_id`, `main_tab`, `slowmode_seconds` and
/// `boosts_unrestrict`.
/// </summary>
public interface IChannelAdminRepository
{
    public bool PutState(TLChannelAdminState state);
    public ValueTask<TLChannelAdminState?> GetStateAsync(long channelId);
    public bool DeleteState(long channelId);

    /// <summary>
    /// Every channel that currently records administration state, in ascending
    /// channel id. Catalogue and statistics reads need the whole set.
    /// </summary>
    public ValueTask<IReadOnlyCollection<TLChannelAdminState>> GetStatesAsync();

    public bool PutSlowModeState(TLChannelSlowModeState state);

    public ValueTask<TLChannelSlowModeState?> GetSlowModeStateAsync(
        long channelId, long userId);

    public bool DeleteSlowModeState(long channelId, long userId);

    /// <summary>
    /// Drops every per-user deadline for one channel, which is what turning slow
    /// mode off means: the next send is immediate for everyone, not merely for
    /// whoever has not posted yet.
    /// </summary>
    public ValueTask<bool> DeleteSlowModeStatesAsync(long channelId);
}
