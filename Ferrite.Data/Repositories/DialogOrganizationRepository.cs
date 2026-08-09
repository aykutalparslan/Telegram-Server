// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public sealed class DialogOrganizationRepository : IDialogOrganizationRepository
{
    private readonly IKVStore _peers;
    private readonly IKVStore _filters;
    private readonly IKVStore _settings;
    private readonly IKVStore _invites;
    private readonly IKVStore _imports;

    public DialogOrganizationRepository(IKVStore peers, IKVStore filters,
        IKVStore settings, IKVStore invites, IKVStore imports)
    {
        _peers = peers;
        _filters = filters;
        _settings = settings;
        _invites = invites;
        _imports = imports;

        peers.SetSchema(new TableDefinition("ferrite", "dialog_peer_state",
            new KeyDefinition("pk",
                new DataColumn { Name = "user_id", Type = DataType.Long },
                new DataColumn { Name = "peer_type", Type = DataType.Int },
                new DataColumn { Name = "peer_id", Type = DataType.Long })));
        filters.SetSchema(new TableDefinition("ferrite", "dialog_filters",
            new KeyDefinition("pk",
                new DataColumn { Name = "user_id", Type = DataType.Long },
                new DataColumn { Name = "filter_id", Type = DataType.Int })));
        settings.SetSchema(new TableDefinition("ferrite", "dialog_filter_settings",
            new KeyDefinition("pk",
                new DataColumn { Name = "user_id", Type = DataType.Long })));
        invites.SetSchema(new TableDefinition("ferrite", "chatlist_invites",
            new KeyDefinition("pk",
                new DataColumn { Name = "owner_user_id", Type = DataType.Long },
                new DataColumn { Name = "filter_id", Type = DataType.Int },
                new DataColumn { Name = "slug", Type = DataType.String }),
            new KeyDefinition("by_slug",
                new DataColumn { Name = "slug", Type = DataType.String })));
        imports.SetSchema(new TableDefinition("ferrite", "imported_chatlists",
            new KeyDefinition("pk",
                new DataColumn { Name = "user_id", Type = DataType.Long },
                new DataColumn { Name = "filter_id", Type = DataType.Int })));
    }

    public bool PutPeerState(TLDialogPeerState state)
    {
        var row = state.AsDialogPeerState();
        return _peers.Put(state.AsSpan().ToArray(), row.UserId, row.PeerType,
            row.PeerId);
    }

    public async ValueTask<TLDialogPeerState?> GetPeerStateAsync(long userId,
        int peerType, long peerId) => WrapPeer(await _peers.GetAsync(userId,
        peerType, peerId));

    public async ValueTask<IReadOnlyCollection<TLDialogPeerState>> GetPeerStatesAsync(
        long userId)
    {
        var rows = new List<TLDialogPeerState>();
        await foreach (byte[] bytes in _peers.IterateAsync(userId))
        {
            rows.Add(new TLDialogPeerState(bytes, 0, bytes.Length));
        }
        return rows;
    }

    public bool DeletePeerState(long userId, int peerType, long peerId) =>
        _peers.Delete(userId, peerType, peerId);

    public bool PutFilter(TLDialogFilterState state)
    {
        var row = state.AsDialogFilterState();
        return _filters.Put(state.AsSpan().ToArray(), row.UserId, row.FilterId);
    }

    public async ValueTask<TLDialogFilterState?> GetFilterAsync(long userId,
        int filterId) => WrapFilter(await _filters.GetAsync(userId, filterId));

    public async ValueTask<IReadOnlyCollection<TLDialogFilterState>> GetFiltersAsync(
        long userId)
    {
        var rows = new List<TLDialogFilterState>();
        await foreach (byte[] bytes in _filters.IterateAsync(userId))
        {
            rows.Add(new TLDialogFilterState(bytes, 0, bytes.Length));
        }
        return rows;
    }

    public bool DeleteFilter(long userId, int filterId) =>
        _filters.Delete(userId, filterId);

    public bool PutSettings(TLDialogFilterSettings settings)
    {
        var row = settings.AsDialogFilterSettings();
        return _settings.Put(settings.AsSpan().ToArray(), row.UserId);
    }

    public async ValueTask<TLDialogFilterSettings?> GetSettingsAsync(long userId) =>
        WrapSettings(await _settings.GetAsync(userId));

    public bool PutInvite(TLChatlistInviteState invite)
    {
        var row = invite.AsChatlistInviteState();
        return _invites.Put(invite.AsSpan().ToArray(), row.OwnerUserId,
            row.FilterId, Encoding.UTF8.GetString(row.Slug));
    }

    public async ValueTask<TLChatlistInviteState?> GetInviteAsync(long ownerUserId,
        int filterId, string slug) => WrapInvite(await _invites.GetAsync(ownerUserId,
        filterId, slug));

    public async ValueTask<TLChatlistInviteState?> GetInviteBySlugAsync(string slug) =>
        WrapInvite(await _invites.GetBySecondaryIndexAsync("by_slug", slug));

    public async ValueTask<IReadOnlyCollection<TLChatlistInviteState>> GetInvitesAsync(
        long ownerUserId, int filterId)
    {
        var rows = new List<TLChatlistInviteState>();
        await foreach (byte[] bytes in _invites.IterateAsync(ownerUserId, filterId))
        {
            rows.Add(new TLChatlistInviteState(bytes, 0, bytes.Length));
        }
        return rows;
    }

    public bool DeleteInvite(long ownerUserId, int filterId, string slug) =>
        _invites.Delete(ownerUserId, filterId, slug);

    public bool PutImport(TLImportedChatlistState import)
    {
        var row = import.AsImportedChatlistState();
        return _imports.Put(import.AsSpan().ToArray(), row.UserId, row.FilterId);
    }

    public async ValueTask<TLImportedChatlistState?> GetImportAsync(long userId,
        int filterId) => WrapImport(await _imports.GetAsync(userId, filterId));

    public async ValueTask<IReadOnlyCollection<TLImportedChatlistState>> GetImportsAsync(
        long userId)
    {
        var rows = new List<TLImportedChatlistState>();
        await foreach (byte[] bytes in _imports.IterateAsync(userId))
        {
            rows.Add(new TLImportedChatlistState(bytes, 0, bytes.Length));
        }
        return rows;
    }

    public bool DeleteImport(long userId, int filterId) =>
        _imports.Delete(userId, filterId);

    private static TLDialogPeerState? WrapPeer(byte[]? bytes) =>
        bytes is { Length: > 0 } ? new TLDialogPeerState(bytes, 0, bytes.Length) : null;

    private static TLDialogFilterState? WrapFilter(byte[]? bytes) =>
        bytes is { Length: > 0 } ? new TLDialogFilterState(bytes, 0, bytes.Length) : null;

    private static TLDialogFilterSettings? WrapSettings(byte[]? bytes) =>
        bytes is { Length: > 0 }
            ? new TLDialogFilterSettings(bytes, 0, bytes.Length) : null;

    private static TLChatlistInviteState? WrapInvite(byte[]? bytes) =>
        bytes is { Length: > 0 }
            ? new TLChatlistInviteState(bytes, 0, bytes.Length) : null;

    private static TLImportedChatlistState? WrapImport(byte[]? bytes) =>
        bytes is { Length: > 0 }
            ? new TLImportedChatlistState(bytes, 0, bytes.Length) : null;
}
