// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public interface IChatInvitesRepository
{
    public bool PutInvite(TLChatInviteInfo invite);
    public ValueTask<TLChatInviteInfo?> GetInviteAsync(long chatId, string hash);
    public TLChatInviteInfo? GetInviteByHash(string hash);
    public ValueTask<IReadOnlyCollection<TLChatInviteInfo>> GetInvitesAsync(long chatId);
    public bool DeleteInvite(long chatId, string hash);
    public bool DeleteInvites(long chatId);
    public bool PutImporter(TLChatInviteImporterInfo importer);
    public ValueTask<IReadOnlyCollection<TLChatInviteImporterInfo>> GetImportersAsync(long chatId);
    public bool DeleteImporters(long chatId);
    public bool PutPendingImporter(TLPendingChatInviteImporter importer);
    public ValueTask<TLPendingChatInviteImporter?> GetPendingImporterAsync(long chatId,
        long userId);
    public ValueTask<IReadOnlyCollection<TLPendingChatInviteImporter>>
        GetPendingImportersAsync(long chatId);
    public bool DeletePendingImporter(long chatId, long userId);
    public bool DeletePendingImporters(long chatId);
}
