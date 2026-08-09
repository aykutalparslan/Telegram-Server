// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Security.Cryptography;
using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services;

public readonly record struct PendingInviteImporter(long ChatId, long UserId,
    int Date, string Link, string About);

// Shared invite-link helpers: hash/link mapping plus the stored dto.chatInviteInfo
// row that wraps the wire chatInviteExported object with its chat id and hash.
public static class ChatInvites
{
    private const string LinkPrefix = "https://t.me/+";

    // Invite hashes match Telegram's [\w-]+ link segment (base64url alphabet).
    public static string GenerateHash()
    {
        Span<byte> randomBytes = stackalloc byte[16];
        RandomNumberGenerator.Fill(randomBytes);
        return Convert.ToBase64String(randomBytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    public static string LinkFromHash(string hash) => LinkPrefix + hash;

    // Accepts a full invite link ("https://t.me/+HASH", ".../joinchat/HASH") or a
    // bare hash and returns the hash segment.
    public static string HashFromLink(string link)
    {
        int plus = link.LastIndexOf('+');
        if (plus >= 0)
        {
            return link[(plus + 1)..];
        }
        const string joinChat = "joinchat/";
        int joinChatIndex = link.LastIndexOf(joinChat, StringComparison.Ordinal);
        if (joinChatIndex >= 0)
        {
            return link[(joinChatIndex + joinChat.Length)..];
        }
        int slash = link.LastIndexOf('/');
        return slash >= 0 ? link[(slash + 1)..] : link;
    }

    // Builds the stored dto row wrapping a chatInviteExported with the given state.
    // Zero expire/usage values keep the corresponding optional flags unset.
    public static TLChatInviteInfo BuildInviteInfo(long chatId, string hash, long adminId,
        int date, bool permanent, bool revoked, bool requestNeeded, int expireDate,
        int usageLimit, int usage, byte[]? title, int requested = 0)
    {
        var inviteBuilder = ChatInviteExported.Builder()
            .Link(Encoding.UTF8.GetBytes(LinkFromHash(hash)))
            .AdminId(adminId)
            .Date(date);
        if (permanent)
        {
            inviteBuilder = inviteBuilder.Permanent(true);
        }
        if (revoked)
        {
            inviteBuilder = inviteBuilder.Revoked(true);
        }
        if (requestNeeded)
        {
            inviteBuilder = inviteBuilder.RequestNeeded(true);
        }
        if (expireDate > 0)
        {
            inviteBuilder = inviteBuilder.ExpireDate(expireDate);
        }
        if (usageLimit > 0)
        {
            inviteBuilder = inviteBuilder.UsageLimit(usageLimit);
        }
        if (usage > 0)
        {
            inviteBuilder = inviteBuilder.Usage(usage);
        }
        if (requested > 0)
        {
            inviteBuilder = inviteBuilder.Requested(requested);
        }
        if (title is { Length: > 0 })
        {
            inviteBuilder = inviteBuilder.Title(title);
        }

        using TLExportedChatInvite invite = inviteBuilder.Build();
        return ChatInviteInfo.Builder()
            .ChatId(chatId)
            .Hash(Encoding.UTF8.GetBytes(hash))
            .Invite(invite.AsSpan())
            .Build();
    }

    // Newly created groups and channels already have a default permanent link.
    public static TLChatInviteInfo CreateDefaultPermanentInvite(long chatId, long adminId,
        int date) =>
        BuildInviteInfo(chatId, GenerateHash(), adminId, date, permanent: true,
            revoked: false, requestNeeded: false, expireDate: 0, usageLimit: 0,
            usage: 0, title: null);

    // The chat's current (non-revoked) permanent invite as serialized wire bytes.
    public static async Task<byte[]?> GetPermanentInviteBytesAsync(
        IChatInvitesRepository invites, long chatId)
    {
        var inviteRows = await invites.GetInvitesAsync(chatId);
        byte[]? result = null;
        foreach (var inviteRow in inviteRows)
        {
            using var row = inviteRow;
            if (result != null)
            {
                continue;
            }
            var info = row.AsChatInviteInfo();
            if (info.Get_InviteView().Is(out ChatInviteExported invite) &&
                invite.Permanent && !invite.Revoked)
            {
                result = info.Invite.ToArray();
            }
        }

        return result;
    }

    public static async Task<List<PendingInviteImporter>> GetPendingImportersAsync(
        IChatInvitesRepository invites, long chatId)
    {
        IReadOnlyCollection<TLPendingChatInviteImporter> rows =
            await invites.GetPendingImportersAsync(chatId);
        var result = new List<PendingInviteImporter>(rows.Count);
        foreach (TLPendingChatInviteImporter row in rows)
        {
            using (row)
            {
                var info = row.AsPendingChatInviteImporter();
                result.Add(new PendingInviteImporter(info.ChatId, info.UserId,
                    info.Date, Encoding.UTF8.GetString(info.Link),
                    Encoding.UTF8.GetString(info.About)));
            }
        }
        return result;
    }
}
