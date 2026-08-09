// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Security.Cryptography;
using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.chatlists;
using Ferrite.TL.baseLayer.dto;
using TLInvite = Ferrite.TL.baseLayer.TLExportedChatlistInvite;

namespace Ferrite.Services;

/// <summary>
/// Owns exported chatlist invite validation, revisioning, and hydration.
/// Durable invite/filter values remain generated TL rows.
/// </summary>
public sealed partial class ChatlistInviteStore
{
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IDialogOrganizationRepository _dialogOrganizationRepository;

    private const string InviteUrlPrefix = "https://t.me/addlist/";
    private readonly IUnitOfWork _unitOfWork;
    private readonly DialogOrganizationStore _organization;
    private readonly UpdateFanout _fanout;
    private readonly ChatRowStore _chatRows;
    private readonly IUpdatesService _updates;
    private readonly IUpdatesContextFactory _updatesContexts;
    private readonly TimeProvider _time;

    public ChatlistInviteStore(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IChatRepository chatRepository, IDialogOrganizationRepository dialogOrganizationRepository,
        DialogOrganizationStore organization, UpdateFanout fanout,
        ChatRowStore chatRows, IUpdatesService updates,
        IUpdatesContextFactory updatesContexts, TimeProvider time)
    {
        _chatParticipantsRepository = chatParticipantsRepository;
        _chatRepository = chatRepository;
        _dialogOrganizationRepository = dialogOrganizationRepository;

        _unitOfWork = unitOfWork;
        _organization = organization;
        _fanout = fanout;
        _chatRows = chatRows;
        _updates = updates;
        _updatesContexts = updatesContexts;
        _time = time;
    }

    public async Task<TLBytes> ExportAsync(long userId, int filterId,
        byte[] title, IReadOnlyList<DialogPeerKey> requestedPeers)
    {
        ShareableFilter? filter = await GetShareableFilterAsync(userId, filterId,
            allowPromotion: true);
        if (filter == null)
        {
            return Error("FILTER_ID_INVALID"u8);
        }
        DialogPeerKey[]? peers = await ValidatePeersAsync(userId, filter.Value,
            requestedPeers);
        if (peers == null)
        {
            return Error("PEER_ID_INVALID"u8);
        }

        string? slug = await CreateSlugAsync();
        if (slug == null)
        {
            return InternalError();
        }
        using TLChatlistInviteState row = BuildRow(userId, filterId, slug,
            revision: 1, title, peers, revoked: false);
        using TLDialogFilterState filterRow = BuildFilterRow(userId, filter.Value,
            hasMyInvites: true);
        if (!_dialogOrganizationRepository.PutInvite(row) ||
            !_dialogOrganizationRepository.PutFilter(filterRow) ||
            !await _unitOfWork.SaveAsync())
        {
            return InternalError();
        }

        byte[] resultFilter = filterRow.AsDialogFilterState().Filter.ToArray();
        using TLInvite invite = BuildInvite(slug, title, peers);
        ChatlistsExportedChatlistInvite result =
            ChatlistsExportedChatlistInvite.Builder().Filter(resultFilter)
                .Invite(invite.AsSpan()).Build();
        return result.TLBytes!.Value;
    }

    public async Task<TLBytes> EditAsync(long userId, int filterId, string slug,
        byte[]? title, IReadOnlyList<DialogPeerKey>? requestedPeers)
    {
        ShareableFilter? filter = await GetShareableFilterAsync(userId, filterId);
        if (filter == null)
        {
            return Error("FILTER_ID_INVALID"u8);
        }
        using TLChatlistInviteState? stored = await _dialogOrganizationRepository.GetInviteAsync(userId, filterId, slug);
        if (stored == null || stored.Value.AsChatlistInviteState().Revoked)
        {
            return Error("INVITE_SLUG_INVALID"u8);
        }

        var current = stored.Value.AsChatlistInviteState();
        byte[] nextTitle = title ?? current.Title.ToArray();
        int revision = checked(current.Revision + 1);
        DialogPeerKey[] nextPeers;
        if (requestedPeers == null)
        {
            if (!TryReadPeers(current.Peers, out nextPeers))
            {
                return Error("PEER_ID_INVALID"u8);
            }
        }
        else
        {
            DialogPeerKey[]? validated = await ValidatePeersAsync(userId,
                filter.Value, requestedPeers);
            if (validated == null)
            {
                return Error("PEER_ID_INVALID"u8);
            }
            nextPeers = validated;
        }

        using TLChatlistInviteState replacement = BuildRow(userId, filterId, slug,
            revision, nextTitle, nextPeers, revoked: false);
        if (!_dialogOrganizationRepository.PutInvite(replacement) ||
            !await _unitOfWork.SaveAsync())
        {
            return InternalError();
        }
        ExportedChatlistInvite result = BuildInviteValue(slug, nextTitle, nextPeers);
        return result.TLBytes!.Value;
    }

    public async Task<TLBytes> DeleteAsync(long userId, int filterId, string slug)
    {
        ShareableFilter? filter = await GetShareableFilterAsync(userId, filterId);
        if (filter == null)
        {
            return Error("FILTER_ID_INVALID"u8);
        }
        using TLChatlistInviteState? stored = await _dialogOrganizationRepository.GetInviteAsync(userId, filterId, slug);
        if (stored == null || stored.Value.AsChatlistInviteState().Revoked)
        {
            return Error("INVITE_SLUG_INVALID"u8);
        }

        var current = stored.Value.AsChatlistInviteState();
        if (!TryReadPeers(current.Peers, out DialogPeerKey[] peers))
        {
            return InternalError();
        }
        using TLChatlistInviteState revoked = BuildRow(userId, filterId, slug,
            checked(current.Revision + 1), current.Title.ToArray(), peers,
            revoked: true);

        bool hasOtherActiveInvite = false;
        IReadOnlyCollection<TLChatlistInviteState> invites = await _dialogOrganizationRepository.GetInvitesAsync(userId, filterId);
        foreach (TLChatlistInviteState invite in invites)
        {
            using (invite)
            {
                var value = invite.AsChatlistInviteState();
                if (!value.Revoked &&
                    !value.Slug.SequenceEqual(Encoding.UTF8.GetBytes(slug)))
                {
                    hasOtherActiveInvite = true;
                }
            }
        }
        using TLDialogFilterState filterRow = BuildFilterRow(userId, filter.Value,
            hasOtherActiveInvite);
        if (!_dialogOrganizationRepository.PutInvite(revoked) ||
            !_dialogOrganizationRepository.PutFilter(filterRow) ||
            !await _unitOfWork.SaveAsync())
        {
            return InternalError();
        }
        TLBool result = BoolTrue.Builder().Build();
        return result;
    }

    public async Task<TLBytes> GetAsync(long userId, int filterId)
    {
        if (await GetShareableFilterAsync(userId, filterId) == null)
        {
            return Error("FILTER_ID_INVALID"u8);
        }
        IReadOnlyCollection<TLChatlistInviteState> rows = await _dialogOrganizationRepository.GetInvitesAsync(userId, filterId);
        var activeInvites = new List<InviteSnapshot>();
        var chatIds = new HashSet<long>();
        foreach (TLChatlistInviteState row in rows)
        {
            using (row)
            {
                var value = row.AsChatlistInviteState();
                if (value.Revoked ||
                    !TryReadPeers(value.Peers, out DialogPeerKey[] peers))
                {
                    continue;
                }
                string slug = Encoding.UTF8.GetString(value.Slug);
                activeInvites.Add(new InviteSnapshot(slug, value.Title.ToArray(),
                    peers));
                foreach (DialogPeerKey peer in peers) chatIds.Add(peer.Id);
            }
        }
        List<byte[]> chatRows = await _fanout.GetChatBytesForViewerAsync(userId,
            chatIds);
        var inviteValues = new Vector();
        foreach (InviteSnapshot invite in activeInvites)
        {
            using TLInvite value = BuildInvite(invite.Slug, invite.Title,
                invite.Peers);
            inviteValues.AppendTLObject(value.AsSpan());
        }
        var chats = new Vector();
        foreach (byte[] row in chatRows) chats.AppendTLObject(row);
        ExportedInvites result = ExportedInvites.Builder().Invites(inviteValues)
            .Chats(chats).Users(new Vector()).Build();
        return result.TLBytes!.Value;
    }

    private async Task<ShareableFilter?> GetShareableFilterAsync(long userId,
        int filterId, bool allowPromotion = false)
    {
        if (filterId < 2) return null;
        using TLDialogFilterState? stored = await _dialogOrganizationRepository.GetFilterAsync(userId, filterId);
        if (stored == null) return null;
        var row = stored.Value.AsDialogFilterState();
        byte[] filterBytes = row.Filter.ToArray();
        return TryReadShareableFilter(filterBytes, userId, filterId,
            allowPromotion, out HashSet<DialogPeerKey> allowed)
            ? new ShareableFilter(row.Position, row.Date, filterBytes, allowed)
            : null;
    }

    private static bool TryReadShareableFilter(byte[] filterBytes, long userId,
        int filterId, bool allowPromotion,
        out HashSet<DialogPeerKey> allowed)
    {
        allowed = [];
        DialogFilterView view = filterBytes.AsSpan();
        if (view.Is(out DialogFilterChatlist filter))
        {
            if (filter.Id != filterId) return false;
            if (!ReadInputPeers(filter.PinnedPeers, userId, allowed) ||
                !ReadInputPeers(filter.IncludePeers, userId, allowed))
            {
                return false;
            }
            return true;
        }
        if (!allowPromotion || !view.Is(out DialogFilter ordinary) ||
            ordinary.Id != filterId || ordinary.ExcludePeers.Count != 0 ||
            ordinary.Contacts || ordinary.NonContacts || ordinary.Groups ||
            ordinary.Broadcasts || ordinary.Bots || ordinary.ExcludeMuted ||
            ordinary.ExcludeRead || ordinary.ExcludeArchived)
        {
            return false;
        }
        if (!ReadInputPeers(ordinary.PinnedPeers, userId, allowed) ||
            !ReadInputPeers(ordinary.IncludePeers, userId, allowed))
        {
            return false;
        }
        return true;
    }

    private async Task<DialogPeerKey[]?> ValidatePeersAsync(long userId,
        ShareableFilter filter, IReadOnlyList<DialogPeerKey> requested)
    {
        var result = new List<DialogPeerKey>(requested.Count);
        var seen = new HashSet<DialogPeerKey>();
        foreach (DialogPeerKey peer in requested)
        {
            if (!seen.Add(peer)) continue;
            // Telegram migrates basic groups during export. Ferrite deliberately
            // does not implement that migration, so only channel-backed groups
            // and broadcast channels are eligible here.
            if (peer.Type != TLPeer.PeerType.PeerChannel ||
                !filter.AllowedPeers.Contains(peer) ||
                !await _organization.CanUsePeerAsync(userId, peer))
            {
                return null;
            }
            result.Add(peer);
        }
        return result.ToArray();
    }

    private async Task<string?> CreateSlugAsync()
    {
        for (int attempt = 0; attempt < 8; attempt++)
        {
            string slug = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
            using TLChatlistInviteState? existing = await _dialogOrganizationRepository.GetInviteBySlugAsync(slug);
            if (existing == null) return slug;
        }
        return null;
    }

    private TLChatlistInviteState BuildRow(long userId, int filterId, string slug,
        int revision, byte[] title, IReadOnlyList<DialogPeerKey> peers,
        bool revoked)
    {
        var values = BuildPeerVector(peers);
        var builder = ChatlistInviteState.Builder().OwnerUserId(userId)
            .FilterId(filterId).Slug(Encoding.UTF8.GetBytes(slug))
            .Revision(revision).Title(title).Peers(values).Date(Now());
        if (revoked) builder = builder.Revoked(true);
        return builder.Build();
    }

    private static TLDialogFilterState BuildFilterRow(long userId,
        ShareableFilter filter, bool hasMyInvites)
    {
        DialogFilterView view = filter.Bytes.AsSpan();
        TLDialogFilter updated;
        int filterId;
        if (view.Is(out DialogFilterChatlist value))
        {
            filterId = value.Id;
            updated = value.Clone().HasMyInvites(hasMyInvites).Build();
        }
        else
        {
            view.Is(out DialogFilter ordinary);
            filterId = ordinary.Id;
            var builder = DialogFilterChatlist.Builder().Id(ordinary.Id)
                .Title(ordinary.Title).PinnedPeers(ordinary.PinnedPeers)
                .IncludePeers(ordinary.IncludePeers).HasMyInvites(hasMyInvites)
                .TitleNoanimate(ordinary.TitleNoanimate);
            if (ordinary.Flags[25]) builder = builder.Emoticon(ordinary.Emoticon);
            if (ordinary.Flags[27]) builder = builder.Color(ordinary.Color);
            updated = builder.Build();
        }
        using (updated)
        {
            return DialogFilterState.Builder().UserId(userId).FilterId(filterId)
            .Position(filter.Position).Filter(updated.AsSpan()).Date(filter.Date)
            .Build();
        }
    }

    private static TLInvite BuildInvite(string slug, byte[] title,
        IReadOnlyList<DialogPeerKey> peers) => BuildInviteValue(slug, title, peers);

    private static ExportedChatlistInvite BuildInviteValue(string slug,
        byte[] title, IReadOnlyList<DialogPeerKey> peers) =>
        ExportedChatlistInvite.Builder().Title(title)
            .Url(Encoding.UTF8.GetBytes(InviteUrlPrefix + slug))
            .Peers(BuildPeerVector(peers)).Build();

    private static Vector BuildPeerVector(IReadOnlyList<DialogPeerKey> peers)
    {
        var result = new Vector();
        foreach (DialogPeerKey peer in peers)
        {
            using TLPeer value = PeerResolver.BuildPeer(peer.Type, peer.Id);
            result.AppendTLObject(value.AsSpan());
        }
        return result;
    }

    private static bool ReadInputPeers(Vector source, long userId,
        HashSet<DialogPeerKey> result)
    {
        int count = source.Count;
        for (int i = 0; i < count; i++)
        {
            Span<byte> bytes = source.ReadTLObject();
            InputPeerView peer = bytes;
            if (!PeerResolver.TryResolveInputPeerDialogKey(peer, userId,
                    out DialogPeerKey key))
            {
                return false;
            }
            result.Add(key);
        }
        return true;
    }

    private static bool TryReadPeers(Vector source, out DialogPeerKey[] peers)
    {
        var result = new List<DialogPeerKey>(source.Count);
        int count = source.Count;
        for (int i = 0; i < count; i++)
        {
            Span<byte> bytes = source.ReadTLObject();
            PeerView peer = bytes;
            if (!PeerResolver.TryReadPeer(peer, out var value))
            {
                peers = [];
                return false;
            }
            result.Add(new DialogPeerKey(value.Type, value.Id));
        }
        peers = result.ToArray();
        return true;
    }

    private int Now() => (int)_time.GetUtcNow().ToUnixTimeSeconds();

    private static TLBytes Error(ReadOnlySpan<byte> message) =>
        RpcErrorGenerator.GenerateError(400, message);

    private static TLBytes InternalError() =>
        RpcErrorGenerator.GenerateError(500, "INTERNAL_SERVER_ERROR"u8);

    private readonly record struct ShareableFilter(int Position, int Date,
        byte[] Bytes, HashSet<DialogPeerKey> AllowedPeers);

    private readonly record struct InviteSnapshot(string Slug, byte[] Title,
        DialogPeerKey[] Peers);
}
