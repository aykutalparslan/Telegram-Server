// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public interface IDialogOrganizationRepository
{
    bool PutPeerState(TLDialogPeerState state);
    ValueTask<TLDialogPeerState?> GetPeerStateAsync(long userId, int peerType,
        long peerId);
    ValueTask<IReadOnlyCollection<TLDialogPeerState>> GetPeerStatesAsync(long userId);
    bool DeletePeerState(long userId, int peerType, long peerId);

    bool PutFilter(TLDialogFilterState state);
    ValueTask<TLDialogFilterState?> GetFilterAsync(long userId, int filterId);
    ValueTask<IReadOnlyCollection<TLDialogFilterState>> GetFiltersAsync(long userId);
    bool DeleteFilter(long userId, int filterId);

    bool PutSettings(TLDialogFilterSettings settings);
    ValueTask<TLDialogFilterSettings?> GetSettingsAsync(long userId);

    bool PutInvite(TLChatlistInviteState invite);
    ValueTask<TLChatlistInviteState?> GetInviteAsync(long ownerUserId, int filterId,
        string slug);
    ValueTask<TLChatlistInviteState?> GetInviteBySlugAsync(string slug);
    ValueTask<IReadOnlyCollection<TLChatlistInviteState>> GetInvitesAsync(
        long ownerUserId, int filterId);
    bool DeleteInvite(long ownerUserId, int filterId, string slug);

    bool PutImport(TLImportedChatlistState import);
    ValueTask<TLImportedChatlistState?> GetImportAsync(long userId, int filterId);
    ValueTask<IReadOnlyCollection<TLImportedChatlistState>> GetImportsAsync(long userId);
    bool DeleteImport(long userId, int filterId);
}
