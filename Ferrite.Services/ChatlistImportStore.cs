// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.chatlists;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services;

public sealed partial class ChatlistInviteStore
{
    public async Task<TLBytes> CheckAsync(long userId, string slug)
    {
        InviteImportSnapshot? invite = await GetActiveInviteAsync(slug);
        if (invite == null)
        {
            return Error("INVITE_SLUG_INVALID"u8);
        }
        ShareableFilterSnapshot? source = await GetChatlistFilterAsync(
            invite.Value.OwnerUserId, invite.Value.OwnerFilterId);
        if (source == null)
        {
            return Error("INVITE_SLUG_INVALID"u8);
        }

        ImportedSnapshot? imported = await GetImportBySlugAsync(userId, slug);
        if (imported == null)
        {
            DialogPeerKey[] available = await GetJoinablePeersAsync(userId,
                invite.Value.Peers);
            return await BuildInviteBeforeImportAsync(userId, source.Value,
                available);
        }

        ShareableFilterSnapshot? local = await GetChatlistFilterAsync(userId,
            imported.Value.FilterId);
        if (local == null)
        {
            return Error("FILTER_ID_INVALID"u8);
        }
        var missing = new List<DialogPeerKey>();
        var already = new List<DialogPeerKey>();
        foreach (DialogPeerKey peer in invite.Value.Peers)
        {
            if (local.Value.PeerSet.Contains(peer))
            {
                already.Add(peer);
            }
            else if (await CanJoinPeerAsync(userId, peer))
            {
                missing.Add(peer);
            }
        }
        return await BuildInviteAlreadyAsync(userId, imported.Value.FilterId,
            missing, already);
    }

    public async Task<TLBytes> JoinInviteAsync(long authKeyId, long userId,
        string slug, IReadOnlyList<DialogPeerKey> requestedPeers)
    {
        InviteImportSnapshot? invite = await GetActiveInviteAsync(slug);
        if (invite == null)
        {
            return UpdatesError("INVITE_SLUG_INVALID"u8);
        }
        if (await GetImportBySlugAsync(userId, slug) != null)
        {
            return UpdatesError("FILTER_ID_INVALID"u8);
        }
        DialogPeerKey[]? selected = SelectSubset(requestedPeers,
            invite.Value.Peers);
        if (selected == null || selected.Length == 0)
        {
            return UpdatesError("PEER_ID_INVALID"u8);
        }
        List<MembershipPlan>? plans = await PlanMembershipAsync(userId, selected);
        if (plans == null)
        {
            return UpdatesError("PEER_ID_INVALID"u8);
        }
        ShareableFilterSnapshot? source = await GetChatlistFilterAsync(
            invite.Value.OwnerUserId, invite.Value.OwnerFilterId);
        if (source == null)
        {
            return UpdatesError("INVITE_SLUG_INVALID"u8);
        }

        (int filterId, int position) = await NextFilterSlotAsync(userId);
        Vector inputPeers = BuildInputPeerVector(plans);
        byte[] filterBytes = BuildImportedFilter(source.Value, filterId, inputPeers);
        using TLDialogFilterState filterRow = DialogFilterState.Builder()
            .UserId(userId).FilterId(filterId).Position(position)
            .Filter(filterBytes).Date(Now()).Build();
        using TLImportedChatlistState importRow = ImportedChatlistState.Builder()
            .UserId(userId).FilterId(filterId)
            .OwnerUserId(invite.Value.OwnerUserId)
            .OwnerFilterId(invite.Value.OwnerFilterId)
            .Slug(Encoding.UTF8.GetBytes(slug))
            .KnownRevision(invite.Value.Revision).Date(Now()).Build();

        if (!ApplyJoinedMembership(userId, plans) ||
            !_dialogOrganizationRepository.PutFilter(filterRow) ||
            !_dialogOrganizationRepository.PutImport(importRow) ||
            !await _unitOfWork.SaveAsync())
        {
            return InternalError();
        }

        byte[] filterUpdate = BuildFilterUpdate(filterId, filterBytes);
        await EnqueueAccountUpdateAsync(authKeyId, userId, filterUpdate);
        await PushChannelUpdatesAsync(userId, plans.Where(x => !x.WasActive));
        return await BuildMembershipUpdatesAsync(authKeyId, userId, filterUpdate,
            plans.Select(x => x.Peer));
    }

    public async Task<TLBytes> GetUpdatesAsync(long userId, int filterId)
    {
        ImportContext? context = await GetImportContextAsync(userId, filterId);
        if (context == null)
        {
            return RpcErrorGenerator.GenerateError(400, "FILTER_ID_INVALID"u8);
        }
        DialogPeerKey[] missing = await GetMissingPeersAsync(userId,
            context.Value);
        return await BuildChatlistUpdatesAsync(userId, missing);
    }

    public async Task<TLBytes> JoinUpdatesAsync(long authKeyId, long userId,
        int filterId, IReadOnlyList<DialogPeerKey> requestedPeers)
    {
        ImportContext? context = await GetImportContextAsync(userId, filterId);
        if (context == null)
        {
            return UpdatesError("FILTER_ID_INVALID"u8);
        }
        DialogPeerKey[] missing = await GetMissingPeersAsync(userId,
            context.Value);
        DialogPeerKey[]? selected = SelectSubset(requestedPeers, missing);
        if (selected == null || selected.Length == 0)
        {
            return UpdatesError("PEER_ID_INVALID"u8);
        }
        List<MembershipPlan>? plans = await PlanMembershipAsync(userId, selected);
        if (plans == null)
        {
            return UpdatesError("PEER_ID_INVALID"u8);
        }

        Vector included = CopyInputPeers(context.Value.Local.Bytes, plans);
        byte[] filterBytes = BuildFilterWithIncludedPeers(context.Value.Local.Bytes,
            included);
        using TLDialogFilterState filterRow = DialogFilterState.Builder()
            .UserId(userId).FilterId(filterId)
            .Position(context.Value.Local.Position).Filter(filterBytes).Date(Now())
            .Build();
        using TLImportedChatlistState importRow = BuildImportRow(
            context.Value.Import, context.Value.Invite.Revision,
            updatesHidden: false);
        if (!ApplyJoinedMembership(userId, plans) ||
            !_dialogOrganizationRepository.PutFilter(filterRow) ||
            !_dialogOrganizationRepository.PutImport(importRow) ||
            !await _unitOfWork.SaveAsync())
        {
            return InternalError();
        }

        byte[] filterUpdate = BuildFilterUpdate(filterId, filterBytes);
        await EnqueueAccountUpdateAsync(authKeyId, userId, filterUpdate);
        await PushChannelUpdatesAsync(userId, plans.Where(x => !x.WasActive));
        return await BuildMembershipUpdatesAsync(authKeyId, userId, filterUpdate,
            plans.Select(x => x.Peer));
    }

    public async Task<TLBytes> HideUpdatesAsync(long userId, int filterId)
    {
        ImportContext? context = await GetImportContextAsync(userId, filterId);
        if (context == null)
        {
            return Error("FILTER_ID_INVALID"u8);
        }
        using TLImportedChatlistState row = BuildImportRow(context.Value.Import,
            context.Value.Invite.Revision, updatesHidden: true);
        if (!_dialogOrganizationRepository.PutImport(row) ||
            !await _unitOfWork.SaveAsync())
        {
            return InternalError();
        }
        TLBool result = BoolTrue.Builder().Build();
        return result;
    }

    public async Task<TLBytes> GetLeaveSuggestionsAsync(long userId, int filterId)
    {
        ImportedSnapshot? import = await GetImportAsync(userId, filterId);
        ShareableFilterSnapshot? filter = await GetChatlistFilterAsync(userId,
            filterId);
        if (import == null || filter == null)
        {
            return Error("FILTER_ID_INVALID"u8);
        }
        Vector peers = BuildPeerVector(filter.Value.Peers);
        byte[] bytes = peers.ToReadOnlySpan().ToArray();
        return new TLBytes(bytes, 0, bytes.Length);
    }

    public async Task<TLBytes> LeaveAsync(long authKeyId, long userId, int filterId,
        IReadOnlyList<DialogPeerKey> requestedPeers)
    {
        ImportedSnapshot? import = await GetImportAsync(userId, filterId);
        ShareableFilterSnapshot? filter = await GetChatlistFilterAsync(userId,
            filterId);
        if (import == null || filter == null)
        {
            return UpdatesError("FILTER_ID_INVALID"u8);
        }
        DialogPeerKey[]? selected = SelectSubset(requestedPeers,
            filter.Value.Peers);
        if (selected == null)
        {
            return UpdatesError("PEER_ID_INVALID"u8);
        }
        List<MembershipPlan>? plans = await PlanMembershipAsync(userId, selected,
            allowBanned: true);
        if (plans == null)
        {
            return UpdatesError("PEER_ID_INVALID"u8);
        }

        if (!ApplyLeftMembership(userId, plans) ||
            !_dialogOrganizationRepository.DeleteImport(userId,
                filterId) ||
            !_dialogOrganizationRepository.DeleteFilter(userId,
                filterId) || !await _unitOfWork.SaveAsync())
        {
            return InternalError();
        }

        byte[] filterUpdate = BuildFilterUpdate(filterId, filterBytes: null);
        await EnqueueAccountUpdateAsync(authKeyId, userId, filterUpdate);
        await PushChannelUpdatesAsync(userId, plans.Where(x => x.WasActive));
        return await BuildMembershipUpdatesAsync(authKeyId, userId, filterUpdate,
            plans.Select(x => x.Peer));
    }

    private async Task<InviteImportSnapshot?> GetActiveInviteAsync(string slug)
    {
        using TLChatlistInviteState? stored = await _dialogOrganizationRepository.GetInviteBySlugAsync(slug);
        if (stored == null) return null;
        var value = stored.Value.AsChatlistInviteState();
        if (value.Revoked || !TryReadPeers(value.Peers,
                out DialogPeerKey[] peers))
        {
            return null;
        }
        return new InviteImportSnapshot(value.OwnerUserId, value.FilterId,
            Encoding.UTF8.GetString(value.Slug), value.Revision, peers);
    }

    private async Task<ImportedSnapshot?> GetImportAsync(long userId, int filterId)
    {
        using TLImportedChatlistState? stored = await _dialogOrganizationRepository.GetImportAsync(userId, filterId);
        return stored == null ? null : ReadImport(stored.Value);
    }

    private async Task<ImportedSnapshot?> GetImportBySlugAsync(long userId,
        string slug)
    {
        IReadOnlyCollection<TLImportedChatlistState> rows = await _dialogOrganizationRepository.GetImportsAsync(userId);
        ImportedSnapshot? result = null;
        foreach (TLImportedChatlistState row in rows)
        {
            using (row)
            {
                ImportedSnapshot current = ReadImport(row);
                if (current.Slug == slug) result = current;
            }
        }
        return result;
    }

    private async Task<ShareableFilterSnapshot?> GetChatlistFilterAsync(long userId,
        int filterId)
    {
        using TLDialogFilterState? stored = await _dialogOrganizationRepository.GetFilterAsync(userId, filterId);
        if (stored == null) return null;
        var row = stored.Value.AsDialogFilterState();
        byte[] filterBytes = row.Filter.ToArray();
        DialogFilterView view = filterBytes.AsSpan();
        if (!view.Is(out DialogFilterChatlist filter) || filter.Id != filterId ||
            !TryReadFilterPeers(filter, userId, out DialogPeerKey[] peers))
        {
            return null;
        }
        return new ShareableFilterSnapshot(row.Position, row.Date, filterBytes,
            peers, peers.ToHashSet());
    }

    private async Task<ImportContext?> GetImportContextAsync(long userId,
        int filterId)
    {
        ImportedSnapshot? import = await GetImportAsync(userId, filterId);
        if (import == null) return null;
        InviteImportSnapshot? invite = await GetActiveInviteAsync(import.Value.Slug);
        if (invite == null || invite.Value.OwnerUserId != import.Value.OwnerUserId ||
            invite.Value.OwnerFilterId != import.Value.OwnerFilterId)
        {
            return null;
        }
        ShareableFilterSnapshot? local = await GetChatlistFilterAsync(userId,
            filterId);
        return local == null ? null : new ImportContext(import.Value,
            invite.Value, local.Value);
    }

    private async Task<DialogPeerKey[]> GetMissingPeersAsync(long userId,
        ImportContext context)
    {
        if (context.Invite.Revision <= context.Import.KnownRevision) return [];
        var result = new List<DialogPeerKey>();
        foreach (DialogPeerKey peer in context.Invite.Peers)
        {
            if (!context.Local.PeerSet.Contains(peer) &&
                await CanJoinPeerAsync(userId, peer))
            {
                result.Add(peer);
            }
        }
        return result.ToArray();
    }

    private async Task<DialogPeerKey[]> GetJoinablePeersAsync(long userId,
        IEnumerable<DialogPeerKey> peers)
    {
        var result = new List<DialogPeerKey>();
        foreach (DialogPeerKey peer in peers)
        {
            if (await CanJoinPeerAsync(userId, peer)) result.Add(peer);
        }
        return result.ToArray();
    }

    private async Task<bool> CanJoinPeerAsync(long userId, DialogPeerKey peer)
    {
        MembershipPlan? plan = await PlanPeerAsync(userId, peer,
            allowBanned: false);
        return plan != null;
    }

    private async Task<List<MembershipPlan>?> PlanMembershipAsync(long userId,
        IEnumerable<DialogPeerKey> peers, bool allowBanned = false)
    {
        var result = new List<MembershipPlan>();
        foreach (DialogPeerKey peer in peers)
        {
            MembershipPlan? plan = await PlanPeerAsync(userId, peer, allowBanned);
            if (plan == null) return null;
            result.Add(plan.Value);
        }
        return result;
    }

    private async Task<MembershipPlan?> PlanPeerAsync(long userId,
        DialogPeerKey peer, bool allowBanned)
    {
        if (peer.Type != TLPeer.PeerType.PeerChannel || peer.Id <= 0) return null;
        byte[] chatBytes;
        long accessHash;
        using (TLChat? chat = await _chatRepository.GetChatAsync(peer.Id))
        {
            if (chat == null || chat.Value.Type != TLChat.ChatType.Channel)
            {
                return null;
            }
            var channel = chat.Value.AsChannel();
            chatBytes = chat.Value.AsSpan().ToArray();
            accessHash = channel.AccessHash;
        }
        bool active = false;
        bool banned = false;
        long inviterId = userId;
        int participantDate = Now();
        using (TLChatParticipantInfo? participant = await _chatParticipantsRepository.GetParticipantAsync(peer.Id, userId))
        {
            if (participant != null)
            {
                int role = participant.Value.AsChatParticipantInfo().Role;
                inviterId = participant.Value.AsChatParticipantInfo().InviterId;
                participantDate = participant.Value.AsChatParticipantInfo().Date;
                active = role is not ((int)ChatParticipantRole.Banned) and
                    not ((int)ChatParticipantRole.Left);
                banned = role == (int)ChatParticipantRole.Banned;
            }
        }
        if (banned && !allowBanned) return null;
        return new MembershipPlan(peer, accessHash, chatBytes, active, banned,
            inviterId, participantDate);
    }

    private bool ApplyJoinedMembership(long userId,
        IReadOnlyCollection<MembershipPlan> plans)
    {
        bool success = true;
        foreach (MembershipPlan plan in plans.Where(x => !x.WasActive))
        {
            using TLChatParticipantInfo joined = ChatParticipantInfo.Builder()
                .ChatId(plan.Peer.Id).UserId(userId)
                .Role((int)ChatParticipantRole.Member).InviterId(userId)
                .Date(Now()).Build();
            success &= _chatParticipantsRepository.PutParticipant(joined);
            _chatRows.UpdateStoredChannelParticipantsCount(plan.ChatBytes, 1);
        }
        return success;
    }

    private bool ApplyLeftMembership(long userId,
        IReadOnlyCollection<MembershipPlan> plans)
    {
        bool success = true;
        foreach (MembershipPlan plan in plans.Where(x => x.WasActive))
        {
            using TLChatParticipantInfo left = ChatParticipantInfo.Builder()
                .ChatId(plan.Peer.Id).UserId(userId)
                .Role((int)ChatParticipantRole.Left).InviterId(plan.InviterId)
                .Date(plan.ParticipantDate).Build();
            success &= _chatParticipantsRepository.PutParticipant(left);
            _chatRows.UpdateStoredChannelParticipantsCount(plan.ChatBytes, -1);
        }
        return success;
    }

    private async Task<TLBytes> BuildInviteBeforeImportAsync(long userId,
        ShareableFilterSnapshot source, IReadOnlyList<DialogPeerKey> peers)
    {
        List<byte[]> chatRows = await _fanout.GetChatBytesForViewerAsync(userId,
            peers.Select(x => x.Id));
        DialogFilterView view = source.Bytes.AsSpan();
        view.Is(out DialogFilterChatlist filter);
        byte[] title = filter.Title.ToArray();
        byte[]? emoticon = filter.Flags[25] ? filter.Emoticon.ToArray() : null;
        bool titleNoanimate = filter.TitleNoanimate;
        var chats = BuildObjectVector(chatRows);
        var builder = ChatlistInvite.Builder().Title(title)
            .Peers(BuildPeerVector(peers)).Chats(chats).Users(new Vector());
        if (emoticon != null) builder = builder.Emoticon(emoticon);
        if (titleNoanimate) builder = builder.TitleNoanimate(true);
        ChatlistInvite result = builder.Build();
        return result.TLBytes!.Value;
    }

    private async Task<TLBytes> BuildInviteAlreadyAsync(long userId, int filterId,
        IReadOnlyList<DialogPeerKey> missing,
        IReadOnlyList<DialogPeerKey> already)
    {
        List<byte[]> chatRows = await _fanout.GetChatBytesForViewerAsync(userId,
            missing.Concat(already).Select(x => x.Id).Distinct());
        ChatlistInviteAlready result = ChatlistInviteAlready.Builder()
            .FilterId(filterId).MissingPeers(BuildPeerVector(missing))
            .AlreadyPeers(BuildPeerVector(already))
            .Chats(BuildObjectVector(chatRows)).Users(new Vector()).Build();
        return result.TLBytes!.Value;
    }

    private async Task<TLBytes> BuildChatlistUpdatesAsync(long userId,
        IReadOnlyList<DialogPeerKey> missing)
    {
        List<byte[]> chatRows = await _fanout.GetChatBytesForViewerAsync(userId,
            missing.Select(x => x.Id));
        ChatlistUpdates result = ChatlistUpdates.Builder()
            .MissingPeers(BuildPeerVector(missing))
            .Chats(BuildObjectVector(chatRows)).Users(new Vector()).Build();
        return result.TLBytes!.Value;
    }

    private async Task<TLBytes> BuildMembershipUpdatesAsync(long authKeyId,
        long userId, byte[] filterUpdate, IEnumerable<DialogPeerKey> peers)
    {
        DialogPeerKey[] peerArray = peers.Distinct().ToArray();
        var updateBytes = new List<byte[]> { filterUpdate };
        foreach (DialogPeerKey peer in peerArray)
        {
            using TLUpdate update = UpdateChannel.Builder().ChannelId(peer.Id).Build();
            updateBytes.Add(update.AsSpan().ToArray());
        }
        List<byte[]> chats = await _fanout.GetChatBytesForViewerAsync(userId,
            peerArray.Select(x => x.Id));
        int seq = await _updatesContexts.GetUpdatesContext(authKeyId, userId)
            .IncrementSeq();
        TLUpdates result = _fanout.BuildUpdates(updateBytes, [userId], chats,
            Now(), seq);
        return result;
    }

    private async Task EnqueueAccountUpdateAsync(long authKeyId, long userId,
        byte[] updateBytes)
    {
        await _updates.EnqueueUpdate(userId,
            new TLUpdate(updateBytes, 0, updateBytes.Length),
            UpdateDeliveryScope.ExcludingAuthKeys([authKeyId]));
    }

    private async Task PushChannelUpdatesAsync(long userId,
        IEnumerable<MembershipPlan> plans)
    {
        foreach (MembershipPlan plan in plans)
        {
            await _fanout.PushUpdateChannelToOtherMembersAsync(plan.Peer.Id, userId);
        }
    }

    private async Task<(int FilterId, int Position)> NextFilterSlotAsync(long userId)
    {
        IReadOnlyCollection<TLDialogFilterState> rows = await _dialogOrganizationRepository.GetFiltersAsync(userId);
        var used = new HashSet<int>();
        int position = -1;
        foreach (TLDialogFilterState row in rows)
        {
            using (row)
            {
                var value = row.AsDialogFilterState();
                used.Add(value.FilterId);
                position = Math.Max(position, value.Position);
            }
        }
        int id = 2;
        while (used.Contains(id)) id++;
        return (id, position + 1);
    }

    private static DialogPeerKey[]? SelectSubset(
        IReadOnlyList<DialogPeerKey> requested,
        IReadOnlyCollection<DialogPeerKey> allowed)
    {
        var allowedSet = allowed.ToHashSet();
        var seen = new HashSet<DialogPeerKey>();
        var selected = new List<DialogPeerKey>();
        foreach (DialogPeerKey peer in requested)
        {
            if (!seen.Add(peer)) continue;
            if (!allowedSet.Contains(peer) ||
                peer.Type != TLPeer.PeerType.PeerChannel)
            {
                return null;
            }
            selected.Add(peer);
        }
        return selected.ToArray();
    }

    private static bool TryReadFilterPeers(DialogFilterChatlist filter,
        long userId, out DialogPeerKey[] peers)
    {
        var result = new List<DialogPeerKey>();
        var seen = new HashSet<DialogPeerKey>();
        Vector pinned = filter.PinnedPeers;
        Vector included = filter.IncludePeers;
        if (!AppendInputPeers(ref pinned, userId, seen, result) ||
            !AppendInputPeers(ref included, userId, seen, result))
        {
            peers = [];
            return false;
        }
        peers = result.ToArray();
        return true;
    }

    private static bool AppendInputPeers(ref Vector source, long userId,
        HashSet<DialogPeerKey> seen, List<DialogPeerKey> result)
    {
        int count = source.Count;
        for (int i = 0; i < count; i++)
        {
            InputPeerView peer = source.ReadTLObject();
            if (!PeerResolver.TryResolveInputPeerDialogKey(peer, userId,
                    out DialogPeerKey key))
            {
                return false;
            }
            if (seen.Add(key)) result.Add(key);
        }
        return true;
    }

    private static Vector BuildInputPeerVector(IEnumerable<MembershipPlan> plans)
    {
        var result = new Vector();
        foreach (MembershipPlan plan in plans)
        {
            using TLInputPeer peer = InputPeerChannel.Builder()
                .ChannelId(plan.Peer.Id).AccessHash(plan.AccessHash).Build();
            result.AppendTLObject(peer.AsSpan());
        }
        return result;
    }

    private static byte[] BuildImportedFilter(ShareableFilterSnapshot source,
        int filterId, Vector included)
    {
        DialogFilterView view = source.Bytes.AsSpan();
        view.Is(out DialogFilterChatlist filter);
        using TLDialogFilter result = filter.Clone().Id(filterId)
            .HasMyInvites(false).PinnedPeers(new Vector()).IncludePeers(included)
            .Build();
        return result.AsSpan().ToArray();
    }

    private static Vector CopyInputPeers(byte[] filterBytes,
        IEnumerable<MembershipPlan> appended)
    {
        DialogFilterView view = filterBytes.AsSpan();
        view.Is(out DialogFilterChatlist filter);
        var result = new Vector();
        Vector current = filter.IncludePeers;
        int count = current.Count;
        for (int i = 0; i < count; i++)
        {
            result.AppendTLObject(current.ReadTLObject());
        }
        foreach (MembershipPlan plan in appended)
        {
            using TLInputPeer peer = InputPeerChannel.Builder()
                .ChannelId(plan.Peer.Id).AccessHash(plan.AccessHash).Build();
            result.AppendTLObject(peer.AsSpan());
        }
        return result;
    }

    private static byte[] BuildFilterWithIncludedPeers(byte[] filterBytes,
        Vector included)
    {
        DialogFilterView view = filterBytes.AsSpan();
        view.Is(out DialogFilterChatlist filter);
        using TLDialogFilter result = filter.Clone().IncludePeers(included).Build();
        return result.AsSpan().ToArray();
    }

    private TLImportedChatlistState BuildImportRow(ImportedSnapshot import,
        int knownRevision, bool updatesHidden)
    {
        var builder = ImportedChatlistState.Builder().UserId(import.UserId)
            .FilterId(import.FilterId).OwnerUserId(import.OwnerUserId)
            .OwnerFilterId(import.OwnerFilterId)
            .Slug(Encoding.UTF8.GetBytes(import.Slug))
            .KnownRevision(knownRevision).Date(Now());
        if (updatesHidden) builder = builder.UpdatesHidden(true);
        return builder.Build();
    }

    private static ImportedSnapshot ReadImport(TLImportedChatlistState stored)
    {
        var value = stored.AsImportedChatlistState();
        return new ImportedSnapshot(value.UserId, value.FilterId,
            value.OwnerUserId, value.OwnerFilterId,
            Encoding.UTF8.GetString(value.Slug), value.KnownRevision,
            value.UpdatesHidden);
    }

    private static byte[] BuildFilterUpdate(int filterId, byte[]? filterBytes)
    {
        var builder = UpdateDialogFilter.Builder().Id(filterId);
        if (filterBytes != null) builder = builder.Filter(filterBytes);
        using TLUpdate update = builder.Build();
        return update.AsSpan().ToArray();
    }

    private static Vector BuildObjectVector(IEnumerable<byte[]> rows)
    {
        var result = new Vector();
        foreach (byte[] row in rows) result.AppendTLObject(row);
        return result;
    }

    private static TLBytes UpdatesError(ReadOnlySpan<byte> message) =>
        RpcErrorGenerator.GenerateError(400, message);

    private readonly record struct InviteImportSnapshot(long OwnerUserId,
        int OwnerFilterId, string Slug, int Revision, DialogPeerKey[] Peers);

    private readonly record struct ImportedSnapshot(long UserId, int FilterId,
        long OwnerUserId, int OwnerFilterId, string Slug, int KnownRevision,
        bool UpdatesHidden);

    private readonly record struct ShareableFilterSnapshot(int Position, int Date,
        byte[] Bytes, DialogPeerKey[] Peers, HashSet<DialogPeerKey> PeerSet);

    private readonly record struct ImportContext(ImportedSnapshot Import,
        InviteImportSnapshot Invite, ShareableFilterSnapshot Local);

    private readonly record struct MembershipPlan(DialogPeerKey Peer,
        long AccessHash, byte[] ChatBytes, bool WasActive, bool WasBanned,
        long InviterId, int ParticipantDate);
}
