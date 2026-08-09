// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL;
using Ferrite.TL.baseLayer;

namespace Ferrite.Services.Channels;

/// <summary>One entry of a channel's username collection.</summary>
public readonly record struct ChannelUsername(string Username, bool Editable,
    bool Active);

/// <summary>
/// The channel username collection, and the one place the compact row's two
/// username fields are reconciled.
///
/// `channel#fe685355` carries BOTH `username` and `usernames`, and pinned TDLib
/// treats them as MUTUALLY EXCLUSIVE: `Usernames::Usernames`
/// (`Usernames.cpp:17-28`) discards the whole collection and logs
/// `LOG(ERROR) &lt;&lt; "Receive first username ... with ..."` when a non-empty
/// `username` arrives alongside a non-empty `usernames`. The failure is silent
/// on the wire -- the client simply behaves as though the channel has no
/// username at all -- so every write goes through <see cref="Apply"/>, which
/// emits exactly one of the two forms.
///
/// TDLib rejects two more shapes the same way: an empty username inside the
/// vector, two editable entries, and an editable entry that is not active
/// (`Usernames.cpp:31-45`). The handlers preserve the last of those by refusing
/// to deactivate the editable username, which is also what
/// `Usernames::can_toggle` (`Usernames.cpp:89-97`) refuses client-side.
/// </summary>
public static class ChannelUsernames
{
    /// <summary>
    /// The channel's usernames in stored order. A row in the legacy single
    /// `username` form reads back as one editable, active entry, so callers
    /// never branch on which form the row happens to use.
    /// </summary>
    public static List<ChannelUsername> Read(Channel channel)
    {
        var result = new List<ChannelUsername>();
        if (channel.Flags2[0])
        {
            Vector stored = channel.Usernames;
            for (int i = 0; i < stored.Count; i++)
            {
                var entry = new Username(stored.ReadTLObject());
                result.Add(new ChannelUsername(
                    Encoding.UTF8.GetString(entry.UsernameProperty),
                    entry.Editable, entry.Active));
            }

            return result;
        }

        if (channel.Flags[6] && channel.Username.Length > 0)
        {
            result.Add(new ChannelUsername(
                Encoding.UTF8.GetString(channel.Username), true, true));
        }

        return result;
    }

    /// <summary>
    /// Rebuilds the row around a whole username collection, choosing the form
    /// pinned TDLib can read: a lone active editable username stays in the
    /// legacy `username` field, and anything else becomes the `usernames`
    /// vector with `username` cleared.
    /// </summary>
    public static TLChat Apply(Channel source,
        IReadOnlyList<ChannelUsername> usernames)
    {
        if (usernames.Count == 0)
        {
            return ChannelRows.WithUsernameCollection(source, default, new Vector());
        }

        if (usernames.Count == 1 && usernames[0] is { Editable: true, Active: true })
        {
            return ChannelRows.WithUsernameCollection(source,
                Encoding.UTF8.GetBytes(usernames[0].Username), new Vector());
        }

        var vector = new Vector();
        foreach (ChannelUsername username in usernames)
        {
            var builder = Username.Builder()
                .UsernameProperty(Encoding.UTF8.GetBytes(username.Username));
            if (username.Editable)
            {
                builder = builder.Editable(true);
            }
            if (username.Active)
            {
                builder = builder.Active(true);
            }
            using TLUsername entry = builder.Build();
            vector.AppendTLObject(entry.AsSpan());
        }

        return ChannelRows.WithUsernameCollection(source, default, vector);
    }

    /// <summary>
    /// Replaces the editable username, which is what `channels.updateUsername`
    /// changes. Mirrors `Usernames::change_editable_username`
    /// (`Usernames.cpp:69-84`): an empty value removes the entry, a new one
    /// keeps the existing position, and a channel with no editable username yet
    /// gains one at the front.
    /// </summary>
    public static List<ChannelUsername> WithEditable(
        IReadOnlyList<ChannelUsername> usernames, string username)
    {
        var result = new List<ChannelUsername>(usernames);
        int editable = result.FindIndex(x => x.Editable);
        if (username.Length == 0)
        {
            if (editable >= 0)
            {
                result.RemoveAt(editable);
            }

            return result;
        }

        if (editable >= 0)
        {
            result[editable] = new ChannelUsername(username, true, true);
        }
        else
        {
            result.Insert(0, new ChannelUsername(username, true, true));
        }

        return result;
    }

    /// <summary>
    /// The editable username, or an empty string when the channel is private.
    /// This is the public address `channels.updateUsername` owns and the value
    /// the global username index is keyed by.
    /// </summary>
    public static string Editable(IReadOnlyList<ChannelUsername> usernames)
    {
        foreach (ChannelUsername username in usernames)
        {
            if (username.Editable)
            {
                return username.Username;
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Whether the channel is publicly addressable, which is true of any ACTIVE
    /// username rather than only the editable one.
    /// </summary>
    public static bool HasActive(IReadOnlyList<ChannelUsername> usernames)
    {
        foreach (ChannelUsername username in usernames)
        {
            if (username.Active)
            {
                return true;
            }
        }

        return false;
    }
}
