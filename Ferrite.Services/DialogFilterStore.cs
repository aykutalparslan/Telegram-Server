// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services;

public readonly record struct DialogFilterSnapshot(int Id, int Position,
    byte[] Filter);

/// <summary>
/// Account-scoped custom dialog filters. Durable rows retain generated
/// DialogFilter values; managed snapshots exist only for one operation.
/// </summary>
public sealed class DialogFilterStore
{
    private readonly IChatParticipantsRepository _chatParticipantsRepository;
    private readonly IMessageRepository _messageRepository;

    private readonly IChatRepository _chatRepository;
    private readonly IDialogOrganizationRepository _dialogOrganizationRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly DialogOrganizationStore _organization;
    private readonly IUpdatesService _updates;
    private readonly TimeProvider _time;

    public DialogFilterStore(IUnitOfWork unitOfWork, IChatParticipantsRepository chatParticipantsRepository, IMessageRepository messageRepository, IChatRepository chatRepository, IDialogOrganizationRepository dialogOrganizationRepository,
        DialogOrganizationStore organization, IUpdatesService updates,
        TimeProvider time)
    {
        _chatParticipantsRepository = chatParticipantsRepository;
        _messageRepository = messageRepository;

        _chatRepository = chatRepository;
        _dialogOrganizationRepository = dialogOrganizationRepository;

        _unitOfWork = unitOfWork;
        _organization = organization;
        _updates = updates;
        _time = time;
    }

    public async Task<List<DialogFilterSnapshot>> GetFiltersAsync(long userId)
    {
        IReadOnlyCollection<TLDialogFilterState> rows = await _dialogOrganizationRepository.GetFiltersAsync(userId);
        var result = new List<DialogFilterSnapshot>(rows.Count);
        foreach (TLDialogFilterState row in rows)
        {
            using (row)
            {
                var value = row.AsDialogFilterState();
                result.Add(new DialogFilterSnapshot(value.FilterId, value.Position,
                    value.Filter.ToArray()));
            }
        }
        return result.OrderBy(x => x.Position).ThenBy(x => x.Id).ToList();
    }

    public async Task<bool> GetTagsEnabledAsync(long userId)
    {
        using TLDialogFilterSettings? settings = await _dialogOrganizationRepository.GetSettingsAsync(userId);
        return settings?.AsDialogFilterSettings().TagsEnabled ?? false;
    }

    public async Task<TLBool> UpdateFilterAsync(long authKeyId, long userId,
        int filterId, byte[]? filterBytes)
    {
        if (filterId < 2)
        {
            return Error(400, "FILTER_ID_INVALID"u8);
        }

        if (filterBytes == null)
        {
            using TLDialogFilterState? existing = await _dialogOrganizationRepository.GetFilterAsync(userId, filterId);
            if (existing != null)
            {
                bool relatedDeleted = await DeleteRelatedStateAsync(userId,
                    filterId);
                if (!relatedDeleted || !_dialogOrganizationRepository
                        .DeleteFilter(userId, filterId) ||
                    !await _unitOfWork.SaveAsync())
                {
                    return Error(500, "INTERNAL_SERVER_ERROR"u8);
                }
                TLUpdate deleted = UpdateDialogFilter.Builder().Id(filterId).Build();
                await _updates.EnqueueUpdate(userId, deleted,
                    UpdateDeliveryScope.ExcludingAuthKeys([authKeyId]));
            }
            return BoolTrue.Builder().Build();
        }

        if (!TryNormalize(filterBytes, userId, filterId,
                out byte[] normalized, out DialogPeerKey[] peers))
        {
            return Error(400, "FILTER_INVALID"u8);
        }
        foreach (DialogPeerKey peer in peers)
        {
            if (!await _organization.CanUsePeerAsync(userId, peer))
            {
                return Error(400, "PEER_ID_INVALID"u8);
            }
        }

        List<DialogFilterSnapshot> current = await GetFiltersAsync(userId);
        int position = current.FirstOrDefault(x => x.Id == filterId).Position;
        if (current.All(x => x.Id != filterId))
        {
            position = current.Select(x => x.Position).DefaultIfEmpty(-1).Max() + 1;
        }
        using TLDialogFilterState row = DialogFilterState.Builder().UserId(userId)
            .FilterId(filterId).Position(position).Filter(normalized).Date(Now())
            .Build();
        if (!_dialogOrganizationRepository.PutFilter(row) ||
            !await _unitOfWork.SaveAsync())
        {
            return Error(500, "INTERNAL_SERVER_ERROR"u8);
        }

        TLUpdate update = UpdateDialogFilter.Builder().Id(filterId)
            .Filter(normalized).Build();
        await _updates.EnqueueUpdate(userId, update,
            UpdateDeliveryScope.ExcludingAuthKeys([authKeyId]));
        return BoolTrue.Builder().Build();
    }

    public async Task<TLBool> ReorderAsync(long authKeyId, long userId,
        IReadOnlyList<int> order)
    {
        if (order.Distinct().Count() != order.Count || order.Any(x => x < 0) ||
            order.Count(x => x == 0) > 1)
        {
            return Error(400, "FILTER_ID_INVALID"u8);
        }
        List<DialogFilterSnapshot> current = await GetFiltersAsync(userId);
        int[] customOrder = order.Where(x => x != 0).ToArray();
        if (customOrder.Length != current.Count ||
            !customOrder.ToHashSet().SetEquals(current.Select(x => x.Id)))
        {
            return Error(400, "FILTER_ID_INVALID"u8);
        }

        var byId = current.ToDictionary(x => x.Id);
        for (int position = 0; position < customOrder.Length; position++)
        {
            DialogFilterSnapshot filter = byId[customOrder[position]];
            using TLDialogFilterState row = DialogFilterState.Builder()
                .UserId(userId).FilterId(filter.Id).Position(position)
                .Filter(filter.Filter).Date(Now()).Build();
            if (!_dialogOrganizationRepository.PutFilter(row))
            {
                return Error(500, "INTERNAL_SERVER_ERROR"u8);
            }
        }
        if (!await _unitOfWork.SaveAsync())
        {
            return Error(500, "INTERNAL_SERVER_ERROR"u8);
        }

        var updateOrder = new VectorOfInt();
        foreach (int id in order) updateOrder.Append(id);
        TLUpdate update = UpdateDialogFilterOrder.Builder().Order(updateOrder).Build();
        await _updates.EnqueueUpdate(userId, update,
            UpdateDeliveryScope.ExcludingAuthKeys([authKeyId]));
        return BoolTrue.Builder().Build();
    }

    public async Task<TLBool> ToggleTagsAsync(long authKeyId, long userId,
        bool enabled)
    {
        bool current = await GetTagsEnabledAsync(userId);
        if (current == enabled)
        {
            return BoolTrue.Builder().Build();
        }

        var builder = DialogFilterSettings.Builder().UserId(userId).Date(Now());
        if (enabled) builder = builder.TagsEnabled(true);
        using TLDialogFilterSettings settings = builder.Build();
        if (!_dialogOrganizationRepository.PutSettings(settings) ||
            !await _unitOfWork.SaveAsync())
        {
            return Error(500, "INTERNAL_SERVER_ERROR"u8);
        }
        TLUpdate update = UpdateDialogFilters.Builder().Build();
        await _updates.EnqueueUpdate(userId, update,
            UpdateDeliveryScope.ExcludingAuthKeys([authKeyId]));
        return BoolTrue.Builder().Build();
    }

    public async Task<List<byte[]>> GetSuggestionsAsync(long userId)
    {
        List<DialogFilterSnapshot> filters = await GetFiltersAsync(userId);
        bool groupsRepresented = false;
        bool broadcastsRepresented = false;
        foreach (DialogFilterSnapshot snapshot in filters)
        {
            DialogFilterView view = snapshot.Filter.AsSpan();
            if (view.Is(out DialogFilter filter))
            {
                groupsRepresented |= filter.Groups;
                broadcastsRepresented |= filter.Broadcasts;
            }
        }

        IReadOnlyCollection<TLSavedMessage> messages = await _messageRepository.GetMessagesAsync(userId);
        Dictionary<DialogPeerKey, MessageSnapshot> common = DialogBuilder
            .GroupTopMessagesByPeer(messages);
        bool hasGroups = common.Keys.Any(x => x.Type == TLPeer.PeerType.PeerChat);
        bool hasBroadcasts = false;
        IReadOnlyCollection<TLChatParticipantInfo> memberships = await _chatParticipantsRepository.GetParticipantsByUserAsync(userId);
        foreach (TLChatParticipantInfo membership in memberships)
        {
            using (membership)
            {
                int role = membership.AsChatParticipantInfo().Role;
                if (role is (int)ChatParticipantRole.Banned or
                    (int)ChatParticipantRole.Left)
                {
                    continue;
                }
                long chatId = membership.AsChatParticipantInfo().ChatId;
                using TLChat? chat = await _chatRepository
                    .GetChatAsync(chatId);
                if (chat == null || chat.Value.Type != TLChat.ChatType.Channel)
                {
                    continue;
                }
                var channel = chat.Value.AsChannel();
                hasGroups |= channel.Megagroup && !channel.Broadcast;
                hasBroadcasts |= channel.Broadcast;
            }
        }

        var result = new List<byte[]>();
        if (hasGroups && !groupsRepresented)
        {
            result.Add(BuildSuggestion(groups: true, "Groups"u8,
                "Group chats in one folder"u8, "👥"u8));
        }
        if (hasBroadcasts && !broadcastsRepresented)
        {
            result.Add(BuildSuggestion(groups: false, "Channels"u8,
                "Broadcast channels in one folder"u8, "📢"u8));
        }
        return result;
    }

    private async Task<bool> DeleteRelatedStateAsync(long userId, int filterId)
    {
        bool success = true;
        IReadOnlyCollection<TLChatlistInviteState> invites = await _dialogOrganizationRepository.GetInvitesAsync(userId, filterId);
        foreach (TLChatlistInviteState invite in invites)
        {
            using (invite)
            {
                string slug = Encoding.UTF8.GetString(
                    invite.AsChatlistInviteState().Slug);
                success &= _dialogOrganizationRepository.DeleteInvite(
                    userId, filterId, slug);
            }
        }
        using TLImportedChatlistState? import = await _dialogOrganizationRepository.GetImportAsync(userId, filterId);
        if (import != null)
        {
            success &= _dialogOrganizationRepository.DeleteImport(userId,
                filterId);
        }
        return success;
    }

    private static bool TryNormalize(byte[] bytes, long userId, int filterId,
        out byte[] normalized, out DialogPeerKey[] peers)
    {
        normalized = [];
        peers = [];
        DialogFilterView view = bytes.AsSpan();
        var seen = new HashSet<DialogPeerKey>();
        var all = new List<DialogPeerKey>();
        if (view.Is(out DialogFilter filter))
        {
            if (!filter.Get_TitleView().Is(out TextWithEntities title) ||
                title.Text.Length == 0 || filter.Id != filterId || filter.Flags[27] &&
                filter.Color is < -1 or > 6)
            {
                return false;
            }
            var pinned = new Vector();
            var included = new Vector();
            var excluded = new Vector();
            Vector sourcePinned = filter.PinnedPeers;
            Vector sourceIncluded = filter.IncludePeers;
            Vector sourceExcluded = filter.ExcludePeers;
            if (!NormalizePeers(ref sourcePinned, ref pinned, userId, seen, all) ||
                !NormalizePeers(ref sourceIncluded, ref included, userId, seen, all) ||
                !NormalizePeers(ref sourceExcluded, ref excluded, userId, seen, all))
            {
                return false;
            }
            using var value = filter.Clone().PinnedPeers(pinned)
                .IncludePeers(included).ExcludePeers(excluded).Build();
            normalized = value.ToReadOnlySpan().ToArray();
            peers = all.ToArray();
            return true;
        }
        if (view.Is(out DialogFilterChatlist chatlist))
        {
            if (!chatlist.Get_TitleView().Is(out TextWithEntities chatTitle) ||
                chatTitle.Text.Length == 0 || chatlist.Id != filterId ||
                chatlist.Flags[27] &&
                chatlist.Color is < -1 or > 6)
            {
                return false;
            }
            var pinned = new Vector();
            var included = new Vector();
            Vector sourcePinned = chatlist.PinnedPeers;
            Vector sourceIncluded = chatlist.IncludePeers;
            if (!NormalizePeers(ref sourcePinned, ref pinned, userId, seen, all) ||
                !NormalizePeers(ref sourceIncluded, ref included, userId, seen, all))
            {
                return false;
            }
            using var value = chatlist.Clone().PinnedPeers(pinned)
                .IncludePeers(included).Build();
            normalized = value.ToReadOnlySpan().ToArray();
            peers = all.ToArray();
            return true;
        }
        return false;
    }

    private static bool NormalizePeers(ref Vector source, ref Vector destination,
        long userId, HashSet<DialogPeerKey> seen, List<DialogPeerKey> all)
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
            if (!seen.Add(key)) continue;
            destination.AppendTLObject(bytes);
            all.Add(key);
        }
        return true;
    }

    private static byte[] BuildSuggestion(bool groups, ReadOnlySpan<byte> titleText,
        ReadOnlySpan<byte> description, ReadOnlySpan<byte> emoticon)
    {
        using var title = TextWithEntities.Builder().Text(titleText)
            .Entities(new Vector()).Build();
        var filterBuilder = DialogFilter.Builder().Id(0)
            .Title(title.ToReadOnlySpan()).Emoticon(emoticon)
            .PinnedPeers(new Vector()).IncludePeers(new Vector())
            .ExcludePeers(new Vector());
        filterBuilder = groups
            ? filterBuilder.Groups(true)
            : filterBuilder.Broadcasts(true);
        using var filter = filterBuilder.Build();
        using var suggested = DialogFilterSuggested.Builder()
            .Filter(filter.ToReadOnlySpan()).Description(description).Build();
        return suggested.ToReadOnlySpan().ToArray();
    }

    private int Now() => (int)_time.GetUtcNow().ToUnixTimeSeconds();

    private static TLBool Error(int code, ReadOnlySpan<byte> message) =>
        (TLBool)RpcErrorGenerator.GenerateError(code, message);
}
