// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services;

/// <summary>
/// The server's static default emoji-reaction set. Every reaction carries
/// schema-valid fabricated sticker documents (webp static icon plus tgs
/// animations) because TDLib drops an availableReaction whose documents do not
/// parse as stickers, which would empty its active-reaction list and block
/// messages.sendReaction client-side. The documents are not downloadable; real
/// reaction assets are not served.
/// </summary>
public static class DefaultReactions
{
    // Matches the head of Telegram's default reaction set; tests and TDLib
    // flows react with members of this list.
    public static readonly IReadOnlyList<string> Emoticons = new[]
    {
        "\U0001F44D", // 👍
        "\U0001F44E", // 👎
        "❤",     // ❤
        "\U0001F525", // 🔥
        "\U0001F970", // 🥰
        "\U0001F44F", // 👏
        "\U0001F601", // 😁
        "\U0001F914", // 🤔
        "\U0001F92F", // 🤯
        "\U0001F631", // 😱
        "\U0001F92C", // 🤬
        "\U0001F622", // 😢
        "\U0001F389", // 🎉
        "\U0001F929", // 🤩
        "\U0001F92E", // 🤮
        "\U0001F4A9", // 💩
        "\U0001F64F", // 🙏
        "\U0001F44C", // 👌
        "\U0001F54A", // 🕊
        "\U0001F921", // 🤡
        "\U0001F971", // 🥱
        "\U0001F974", // 🥴
        "\U0001F60D", // 😍
        "\U0001F433", // 🐳
        "\U0001F4AF", // 💯
        "\U0001F923", // 🤣
        "⚡",     // ⚡
        "\U0001F34C", // 🍌
        "\U0001F3C6", // 🏆
        "\U0001F494", // 💔
        "\U0001F610", // 😐
        "\U0001F353", // 🍓
        "\U0001F37E", // 🍾
        "\U0001F48B", // 💋
        "\U0001F595", // 🖕
        "\U0001F608", // 😈
    };

    /// <summary>
    /// Constant content hash for the static set; clients echo it back and get
    /// availableReactionsNotModified while the set is unchanged.
    /// </summary>
    public const int Hash = 214_001;

    private const long DocumentIdBase = 0x21400000000L;
    private static readonly byte[] SerializedAvailableReactions = BuildAvailableReactions();
    private static readonly byte[] SerializedAllChatReactions = BuildAllChatReactions();

    public static ReadOnlySpan<byte> AvailableReactionsBytes => SerializedAvailableReactions;

    /// <summary>
    /// The per-chat default when no available_reactions value is stored:
    /// chatReactionsAll without allow_custom (Telegram's default for new
    /// groups/channels; custom-emoji reactions are premium surface).
    /// </summary>
    public static ReadOnlySpan<byte> AllChatReactionsBytes => SerializedAllChatReactions;

    private static byte[] BuildAllChatReactions()
    {
        using var all = ChatReactionsAll.Builder().Build();
        return all.ToReadOnlySpan().ToArray();
    }

    private static readonly List<byte[]> SerializedDefaultReactions =
        BuildDefaultReactionBytes();

    /// <summary>
    /// The default set as serialized reactionEmoji values, in featured order.
    /// </summary>
    public static IReadOnlyList<byte[]> DefaultReactionBytes => SerializedDefaultReactions;

    private static List<byte[]> BuildDefaultReactionBytes()
    {
        var reactions = new List<byte[]>(Emoticons.Count);
        foreach (string emoji in Emoticons)
        {
            using var reaction = ReactionEmoji.Builder()
                .Emoticon(Encoding.UTF8.GetBytes(emoji))
                .Build();
            reactions.Add(reaction.ToReadOnlySpan().ToArray());
        }

        return reactions;
    }

    public static bool IsDefaultEmoji(ReadOnlySpan<byte> emoticon)
    {
        foreach (string emoji in Emoticons)
        {
            if (emoticon.SequenceEqual(Encoding.UTF8.GetBytes(emoji)))
            {
                return true;
            }
        }

        return false;
    }

    private static byte[] BuildAvailableReactions()
    {
        var reactions = new Vector();
        int date = 1720000000;
        for (int i = 0; i < Emoticons.Count; i++)
        {
            string emoji = Emoticons[i];
            long documentId = DocumentIdBase + i * 16L;
            using var staticIcon = BuildStickerDocument(documentId, emoji, date,
                "image/webp"u8.ToArray());
            using var appearAnimation = BuildStickerDocument(documentId + 1, emoji, date,
                "application/x-tgsticker"u8.ToArray());
            using var selectAnimation = BuildStickerDocument(documentId + 2, emoji, date,
                "application/x-tgsticker"u8.ToArray());
            using var activateAnimation = BuildStickerDocument(documentId + 3, emoji, date,
                "application/x-tgsticker"u8.ToArray());
            using var effectAnimation = BuildStickerDocument(documentId + 4, emoji, date,
                "application/x-tgsticker"u8.ToArray());
            byte[] emojiBytes = Encoding.UTF8.GetBytes(emoji);
            using var reaction = AvailableReaction.Builder()
                .Reaction(emojiBytes)
                .Title(emojiBytes)
                .StaticIcon(staticIcon.AsSpan())
                .AppearAnimation(appearAnimation.AsSpan())
                .SelectAnimation(selectAnimation.AsSpan())
                .ActivateAnimation(activateAnimation.AsSpan())
                .EffectAnimation(effectAnimation.AsSpan())
                .Build();
            reactions.AppendTLObject(reaction.ToReadOnlySpan());
        }

        using var availableReactions = AvailableReactions.Builder()
            .Hash(Hash)
            .Reactions(reactions)
            .Build();
        return availableReactions.ToReadOnlySpan().ToArray();
    }

    private static TLDocument BuildStickerDocument(long documentId, string emoji, int date,
        byte[] mimeType)
    {
        using var stickerSet = InputStickerSetEmpty.Builder().Build();
        using var stickerAttribute = DocumentAttributeSticker.Builder()
            .Alt(Encoding.UTF8.GetBytes(emoji))
            .Stickerset(stickerSet.ToReadOnlySpan())
            .Build();
        using var sizeAttribute = DocumentAttributeImageSize.Builder()
            .W(512)
            .H(512)
            .Build();
        var attributes = new Vector();
        attributes.AppendTLObject(stickerAttribute.ToReadOnlySpan());
        attributes.AppendTLObject(sizeAttribute.ToReadOnlySpan());
        return Document.Builder()
            .Id(documentId)
            .AccessHash(documentId ^ 0x7261636EL)
            .FileReference(new byte[] { 0x21, 0x40, 0x00, 0x01 })
            .Date(date)
            .MimeType(mimeType)
            .Size(1024)
            .DcId(2)
            .Attributes(attributes)
            .Build();
    }
}
