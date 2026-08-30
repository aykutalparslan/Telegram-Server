// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL;
using Ferrite.TL.baseLayer;

namespace Ferrite.Services.Channels;

public readonly record struct ChannelUsername(string Username, bool Editable,
    bool Active);

public static class ChannelUsernames
{
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
