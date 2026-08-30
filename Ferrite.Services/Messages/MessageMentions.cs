// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL;
using Ferrite.TL.baseLayer;

namespace Ferrite.Services.Messages;

public static class MessageMentions
{
    public readonly record struct UsernameToken(int Offset, int Length,
        string Username);

    public static List<UsernameToken> ScanUsernames(ReadOnlySpan<byte> utf8Text) =>
        ScanUsernames(Encoding.UTF8.GetString(utf8Text));

    public static List<UsernameToken> ScanUsernames(string text)
    {
        var tokens = new List<UsernameToken>();
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] != '@' || (i > 0 && IsUsernameChar(text[i - 1])))
            {
                continue;
            }

            int start = i + 1;
            int end = start;
            while (end < text.Length && IsUsernameChar(text[end]))
            {
                end++;
            }
            if (end == start)
            {
                continue;
            }

            tokens.Add(new UsernameToken(i, end - i, text[start..end]));
            i = end - 1;
        }
        return tokens;
    }

    public static void AppendMentionEntities(ref Vector entities,
        IReadOnlyList<UsernameToken> tokens)
    {
        foreach (UsernameToken token in tokens)
        {
            using TLMessageEntity entity = MessageEntityMention.Builder()
                .Offset(token.Offset)
                .LengthProperty(token.Length)
                .Build();
            entities.AppendTLObject(entity.AsSpan());
        }
    }

    public static bool MentionsUser(Message message, long userId, string? username)
    {
        if (!message.Flags[7])
        {
            return false;
        }

        string? text = null;
        Vector entities = message.Entities;
        int count = entities.Count;
        for (int i = 0; i < count; i++)
        {
            var entity = (MessageEntityView)entities.ReadTLObject();
            if (entity.Is(out MessageEntityMentionName named))
            {
                if (named.UserId == userId)
                {
                    return true;
                }
                continue;
            }
            if (username == null || !entity.Is(out MessageEntityMention mention))
            {
                continue;
            }

            text ??= Encoding.UTF8.GetString(message.MessageProperty);
            if (SlicesUsername(text, mention.Offset, mention.LengthProperty,
                    username))
            {
                return true;
            }
        }
        return false;
    }

    public static byte[] StampUnread(byte[] messageBytes)
    {
        using var stored = new TLMessage(messageBytes, 0, messageBytes.Length);
        using TLMessage stamped = stored.AsMessage().Clone()
            .Mentioned(true)
            .MediaUnread(true)
            .Build();
        return stamped.AsSpan().ToArray();
    }

    private static bool SlicesUsername(string text, int offset, int length,
        string username)
    {
        if (offset < 0 || length <= 1 || offset + length > text.Length)
        {
            return false;
        }
        ReadOnlySpan<char> slice = text.AsSpan(offset, length);
        return slice[0] == '@' &&
               slice[1..].Equals(username, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUsernameChar(char value) =>
        value is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9'
            or '_';
}
