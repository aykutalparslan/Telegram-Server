// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Chats;

public sealed record StoredInvite(long ChatId, string Hash, long AdminId, int Date,
    bool Permanent, bool Revoked, bool RequestNeeded, int ExpireDate, int UsageLimit,
    int Usage, int Requested, byte[]? Title, byte[] InviteBytes);

public sealed class InviteStore
{
    private readonly IChatInvitesRepository _chatInvitesRepository;

    private readonly IUnitOfWork _unitOfWork;

    public InviteStore(IUnitOfWork unitOfWork, IChatInvitesRepository chatInvitesRepository)
    {
        _chatInvitesRepository = chatInvitesRepository;

        _unitOfWork = unitOfWork;
    }

    private static StoredInvite? ReadStoredInvite(TLChatInviteInfo row)
    {
        var info = row.AsChatInviteInfo();
        if (!info.Get_InviteView().Is(out ChatInviteExported invite))
        {
            return null;
        }

        return new StoredInvite(info.ChatId, Encoding.UTF8.GetString(info.Hash),
            invite.AdminId, invite.Date, invite.Permanent, invite.Revoked,
            invite.RequestNeeded, invite.ExpireDate, invite.UsageLimit, invite.Usage,
            invite.Requested, invite.Title.Length > 0 ? invite.Title.ToArray() : null,
            info.Invite.ToArray());
    }

    public async Task<List<StoredInvite>> GetStoredInvitesAsync(long chatId)
    {
        var rows = await _chatInvitesRepository.GetInvitesAsync(chatId);
        var invites = new List<StoredInvite>();
        foreach (var row in rows)
        {
            using var r = row;
            var invite = ReadStoredInvite(r);
            if (invite != null)
            {
                invites.Add(invite);
            }
        }

        return invites;
    }

    public async Task<StoredInvite?> GetStoredInviteAsync(long chatId, string hash)
    {
        var row = await _chatInvitesRepository.GetInviteAsync(chatId, hash);
        if (row == null)
        {
            return null;
        }
        var invite = ReadStoredInvite(row.Value);
        row.Value.Dispose();
        return invite;
    }

    public StoredInvite? GetStoredInviteByHash(string hash)
    {
        var row = _chatInvitesRepository.GetInviteByHash(hash);
        if (row == null)
        {
            return null;
        }
        var invite = ReadStoredInvite(row.Value);
        row.Value.Dispose();
        return invite;
    }

    public void PutStoredInvite(StoredInvite invite, bool revoked, bool requestNeeded,
        int expireDate, int usageLimit, int usage, byte[]? title,
        int? requested = null)
    {
        using TLChatInviteInfo row = ChatInvites.BuildInviteInfo(invite.ChatId, invite.Hash,
            invite.AdminId, invite.Date, invite.Permanent, revoked, requestNeeded,
            expireDate, usageLimit, usage, title, requested ?? invite.Requested);
        _chatInvitesRepository.PutInvite(row);
    }
}
