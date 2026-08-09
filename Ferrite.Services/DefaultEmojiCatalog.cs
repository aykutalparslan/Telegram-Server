// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services;

public enum EmojiGroupKind
{
    General,
    Status,
    ProfilePhoto,
    Sticker,
}

public static class DefaultEmojiCatalog
{
    public const int Version = 1;
    public const int GroupsHash = 214_003;

    private static readonly (string Keyword, string[] Emoji)[] Keywords =
    [
        ("like", ["👍", "👌", "💯"]),
        ("love", ["❤", "🥰", "😍", "💋"]),
        ("happy", ["😁", "🤣", "🎉"]),
        ("sad", ["😢", "💔"]),
        ("angry", ["👎", "🤬", "🖕", "😈"]),
        ("wow", ["🤯", "😱"]),
        ("thinking", ["🤔", "😐"]),
        ("fire", ["🔥", "⚡"]),
        ("animals", ["🕊", "🐳"]),
        ("food", ["🍌", "🍓", "🍾"]),
        ("celebration", ["👏", "🎉", "🏆"]),
    ];

    public static TLBytes GetKeywords(int fromVersion)
    {
        var values = new Vector();
        if (fromVersion < Version)
        {
            foreach ((string keyword, string[] emoji) in Keywords)
            {
                using EmojiKeyword value = EmojiKeyword.Builder()
                    .Keyword(Encoding.UTF8.GetBytes(keyword))
                    .Emoticons(Strings(emoji)).Build();
                values.AppendTLObject(value.ToReadOnlySpan());
            }
        }
        var result = EmojiKeywordsDifference.Builder().LangCode("en"u8)
            .FromVersion(Math.Min(fromVersion, Version)).Version(Version)
            .Keywords(values).Build();
        return result.TLBytes!.Value;
    }

    public static TLBytes GetLanguages()
    {
        using EmojiLanguage language = EmojiLanguage.Builder()
            .LangCode("en"u8).Build();
        var values = new Vector();
        values.AppendTLObject(language.ToReadOnlySpan());
        return Copy(values);
    }

    public static TLBytes GetUrl()
    {
        var result = EmojiURL.Builder()
            .Url("https://translations.telegram.org/en/emoji"u8).Build();
        return result.TLBytes!.Value;
    }

    public static TLBytes GetGroups(EmojiGroupKind kind, int requestedHash)
    {
        if (requestedHash == GroupsHash)
        {
            var unchanged = EmojiGroupsNotModified.Builder().Build();
            return unchanged.TLBytes!.Value;
        }
        var groups = new Vector();
        AppendGroup(ref groups, "Faces", 0x21400300001,
            ["😁", "🥰", "🤔", "🤯", "😱", "😢", "😍"]);
        AppendGroup(ref groups, "Celebration", 0x21400300002,
            ["👏", "🎉", "🏆", "🍾"]);
        AppendGroup(ref groups, "Symbols", 0x21400300003,
            ["❤", "🔥", "💯", "⚡", "💔"]);
        AppendGroup(ref groups, "Nature", 0x21400300004,
            ["🕊", "🐳", "🍌", "🍓"]);
        if (kind == EmojiGroupKind.Sticker)
        {
            using EmojiGroupGreeting greeting = EmojiGroupGreeting.Builder()
                .Title("Greetings"u8).IconEmojiId(0x21400300005)
                .Emoticons(Strings(["👍", "👏", "🙏", "💋"])).Build();
            groups.AppendTLObject(greeting.ToReadOnlySpan());
        }
        var result = EmojiGroups.Builder().Hash(GroupsHash).Groups(groups)
            .Build();
        return result.TLBytes!.Value;
    }

    private static void AppendGroup(ref Vector groups, string title,
        long iconId, string[] emoji)
    {
        using EmojiGroup group = EmojiGroup.Builder()
            .Title(Encoding.UTF8.GetBytes(title)).IconEmojiId(iconId)
            .Emoticons(Strings(emoji)).Build();
        groups.AppendTLObject(group.ToReadOnlySpan());
    }

    private static VectorOfString Strings(IEnumerable<string> values)
    {
        var result = new VectorOfString();
        foreach (string value in values)
        {
            result.AppendTLBytes(Encoding.UTF8.GetBytes(value));
        }
        return result;
    }

    private static TLBytes Copy(Vector value)
    {
        byte[] bytes = value.ToReadOnlySpan().ToArray();
        return new TLBytes(bytes, 0, bytes.Length);
    }
}
