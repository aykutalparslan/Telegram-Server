// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.contacts;
using Ferrite.TL.baseLayer.dto;
using PeerBlocked = Ferrite.TL.baseLayer.PeerBlocked;

namespace Ferrite.Services.Handlers.ContactMethods;

public abstract class ContactsHandlerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IUserStatusRepository _userStatusRepository;
    private readonly UserSerializer _userSerializer;

    protected readonly IUnitOfWork _unitOfWork;
    protected readonly ISearchEngine _search;
    protected readonly IUpdatesService _updates;
    protected readonly IUpdatesContextFactory _updatesContextFactory;

    protected ContactsHandlerBase(IUnitOfWork unitOfWork, IContactsRepository contactsRepository, IUserRepository userRepository, IUserStatusRepository userStatusRepository, ISearchEngine search,
        IUpdatesService updates, IUpdatesContextFactory updatesContextFactory)
    {
        _userRepository = userRepository;
        _userStatusRepository = userStatusRepository;
        _userSerializer = new UserSerializer(userRepository, userStatusRepository, contactsRepository);

        _unitOfWork = unitOfWork;
        _search = search;
        _updates = updates;
        _updatesContextFactory = updatesContextFactory;
    }

    protected static TLBytes ToLongVector(IEnumerable<long> values)
    {
        var vector = new VectorOfLong();
        foreach (long value in values) vector.Append(value);
        return CopyVector(vector.ToReadOnlySpan());
    }

    protected static TLBytes ToContactStatusVector(IEnumerable<TLContactStatus> statuses)
    {
        var vector = new Vector();
        foreach (TLContactStatus status in statuses)
        {
            vector.AppendTLObject(status.AsSpan());
            status.Dispose();
        }
        return CopyVector(vector.ToReadOnlySpan());
    }

    protected static TLBytes ToSavedContactVector(IEnumerable<TLSavedContact> savedContacts)
    {
        var vector = new Vector();
        foreach (TLSavedContact savedContact in savedContacts)
        {
            vector.AppendTLObject(savedContact.AsSpan());
            savedContact.Dispose();
        }
        return CopyVector(vector.ToReadOnlySpan());
    }

    protected static TLBytes CopyVector(ReadOnlySpan<byte> value)
    {
        byte[] bytes = value.ToArray();
        return new TLBytes(bytes, 0, bytes.Length);
    }

    protected static Vector ToUserVector(ICollection<TLUser> users)
    {
        var vector = new Vector();
        foreach (TLUser user in users)
        {
            vector.AppendTLObject(user.AsSpan());
            user.Dispose();
        }
        return vector;
    }

    protected static Vector ToContactVector(ICollection<TLContact> contacts)
    {
        var vector = new Vector();
        foreach (TLContact contact in contacts)
        {
            vector.AppendTLObject(contact.AsSpan());
            contact.Dispose();
        }
        return vector;
    }

    protected static Vector ToImportedContactVector(
        ICollection<TLImportedContact> contacts)
    {
        var vector = new Vector();
        foreach (TLImportedContact contact in contacts)
        {
            vector.AppendTLObject(contact.AsSpan());
            contact.Dispose();
        }
        return vector;
    }

    protected readonly record struct InputPhoneContactInfo(
        long ClientId,
        string Phone,
        byte[] PhoneBytes,
        byte[] FirstName,
        byte[] LastName);

    protected static List<InputPhoneContactInfo> ToInputPhoneContactList(
        Vector inputContacts)
    {
        var result = new List<InputPhoneContactInfo>();
        for (int i = 0; i < inputContacts.Count; i++)
        {
            InputContactView contact = inputContacts.ReadTLObject();
            if (!contact.Is(out InputPhoneContact phoneContact)) continue;
            byte[] phoneBytes = phoneContact.Phone.ToArray();
            result.Add(new InputPhoneContactInfo(
                phoneContact.ClientId,
                Encoding.UTF8.GetString(phoneBytes),
                phoneBytes,
                phoneContact.FirstName.ToArray(),
                phoneContact.LastName.ToArray()));
        }
        return result;
    }

    protected ValueTask<TLUser?> GetUserInternal(long viewerUserId, long userId) =>
        _userSerializer.GetAsync(viewerUserId, userId);

    protected ValueTask<TLUser> WithStatus(long viewerUserId, TLUser user) =>
        _userSerializer.WithStatusAsync(viewerUserId, user);

    protected static List<long> ToInputUserIds(Vector inputUsers, long selfUserId)
    {
        var result = new List<long>();
        for (int i = 0; i < inputUsers.Count; i++)
        {
            InputUserView user = inputUsers.ReadTLObject();
            if (user.Is(out InputUser inputUser))
                result.Add(inputUser.UserId);
            else if (user.Is(out InputUserFromMessage fromMessage))
                result.Add(fromMessage.UserId);
            else if (user.Constructor == Constructors.baseLayer_InputUserSelf)
                result.Add(selfUserId);
        }
        return result;
    }

    protected static Vector ToUpdateVector(ICollection<TLUpdate> updates)
    {
        var vector = new Vector();
        foreach (TLUpdate update in updates)
        {
            vector.AppendTLObject(update.AsSpan());
            update.Dispose();
        }
        return vector;
    }

    protected static List<string> ToStringList(VectorOfString values)
    {
        var result = new List<string>();
        for (int i = 0; i < values.Count; i++)
            result.Add(Encoding.UTF8.GetString(values.ReadTLBytes()));
        return result;
    }

    protected readonly record struct BlockedPeerKey(long PeerId, PeerType PeerType);

    protected readonly record struct BlockedPeerEntry(long PeerId, PeerType PeerType,
        int Date);

    protected static BlockedPeerKey? GetBlockedPeer(InputPeerView view, long selfUserId) =>
        view.Type switch
        {
            TLInputPeer.InputPeerType.InputPeerSelf =>
                new BlockedPeerKey(selfUserId, PeerType.User),
            TLInputPeer.InputPeerType.InputPeerUser =>
                new BlockedPeerKey(view.AsInputPeerUser().UserId, PeerType.User),
            TLInputPeer.InputPeerType.InputPeerUserFromMessage =>
                new BlockedPeerKey(view.AsInputPeerUserFromMessage().UserId, PeerType.User),
            TLInputPeer.InputPeerType.InputPeerChat =>
                new BlockedPeerKey(view.AsInputPeerChat().ChatId, PeerType.Chat),
            TLInputPeer.InputPeerType.InputPeerChannel =>
                new BlockedPeerKey(view.AsInputPeerChannel().ChannelId, PeerType.Channel),
            TLInputPeer.InputPeerType.InputPeerChannelFromMessage =>
                new BlockedPeerKey(view.AsInputPeerChannelFromMessage().ChannelId,
                    PeerType.Channel),
            _ => null,
        };

    protected static List<BlockedPeerKey> ToBlockedPeerKeys(Vector peers, int limit,
        long selfUserId)
    {
        var result = new List<BlockedPeerKey>();
        int count = Math.Min(peers.Count, Math.Max(0, limit));
        for (int i = 0; i < count; i++)
        {
            InputPeerView peer = peers.ReadTLObject();
            BlockedPeerKey? blockedPeer = GetBlockedPeer(peer, selfUserId);
            if (blockedPeer != null) result.Add(blockedPeer.Value);
        }
        return result;
    }

    protected static List<BlockedPeerEntry> ReadBlockedPeerEntries(
        IReadOnlyList<TLBlockedPeer> blockedPeers)
    {
        var entries = new List<BlockedPeerEntry>(blockedPeers.Count);
        foreach (TLBlockedPeer blockedPeer in blockedPeers)
        {
            var peer = blockedPeer.AsBlockedPeer();
            entries.Add(new BlockedPeerEntry(peer.PeerId, (PeerType)peer.PeerType,
                peer.Date));
            blockedPeer.Dispose();
        }
        return entries;
    }

    protected static List<BlockedPeerEntry> PageBlockedPeers(
        IReadOnlyList<BlockedPeerEntry> blockedPeers, int offset, int limit)
    {
        int start = Math.Clamp(offset, 0, blockedPeers.Count);
        int take = Math.Max(0, limit);
        return blockedPeers.Skip(start).Take(take).ToList();
    }

    protected static bool ShouldReturnBlockedSlice(int totalCount, int pageCount,
        int offset, int limit) =>
        totalCount > 0 && (offset > 0 || limit < totalCount || pageCount < totalCount);

    protected Vector ToPeerBlockedVector(
        IReadOnlyCollection<BlockedPeerEntry> blockedPeers)
    {
        var vector = new Vector();
        foreach (BlockedPeerEntry blockedPeer in blockedPeers)
        {
            using TLPeer peerId = CreatePeer(blockedPeer.PeerType, blockedPeer.PeerId);
            using var peer = new PeerBlocked(peerId.AsSpan(), blockedPeer.Date);
            vector.AppendTLObject(peer.ToReadOnlySpan());
        }
        return vector;
    }

    protected static TLPeer CreatePeer(PeerType peerType, long peerId) => peerType switch
    {
        PeerType.Channel => new PeerChannel(peerId),
        PeerType.Chat => new PeerChat(peerId),
        _ => new PeerUser(peerId),
    };

    protected async Task EnqueuePeerBlockedUpdate(long ownerUserId, BlockedPeerKey peer,
        bool blocked, bool myStoriesFrom)
    {
        using TLPeer peerId = CreatePeer(peer.PeerType, peer.PeerId);
        TLUpdate update = UpdatePeerBlocked.Builder()
            .Blocked(blocked)
            .BlockedMyStoriesFrom(myStoriesFrom)
            .PeerId(peerId.AsSpan())
            .Build();
        await _updates.EnqueueUpdate(ownerUserId, update);
    }

    protected static Vector ToPeerVector(ICollection<TLPeer> peers)
    {
        var vector = new Vector();
        foreach (TLPeer peer in peers)
        {
            vector.AppendTLObject(peer.AsSpan());
            peer.Dispose();
        }
        return vector;
    }

    protected static TLContacts EmptyContacts() =>
        Ferrite.TL.baseLayer.contacts.Contacts.Builder()
        .ContactsProperty(new Vector())
        .Users(new Vector())
        .SavedCount(0)
        .Build();

    protected static TLImportedContacts EmptyImportedContacts() =>
        ImportedContacts.Builder()
            .Imported(new Vector())
            .PopularInvites(new Vector())
            .RetryContacts(new VectorOfLong())
            .Users(new Vector())
            .Build();

    protected static TLBlocked EmptyBlocked() => Blocked.Builder()
        .Users(new Vector())
        .Chats(new Vector())
        .BlockedProperty(new Vector())
        .Build();

    protected static TLBool AuthKeyInvalidBool() =>
        (TLBool)RpcErrorGenerator.GenerateError(400, "AUTH_KEY_INVALID"u8);

    protected static TLUpdates AuthKeyInvalidUpdates() =>
        (TLUpdates)RpcErrorGenerator.GenerateError(400, "AUTH_KEY_INVALID"u8);

    protected async Task<TLUpdates> BuildPeerSettingsUpdates(long authKeyId,
        long ownerUserId, long peerUserId, ICollection<TLUser> users,
        TLPeerSettings settings)
    {
        using TLPeer peer = new PeerUser(peerUserId);
        TLUpdate update = UpdatePeerSettings.Builder()
            .Peer(peer.AsSpan())
            .Settings(settings.AsSpan())
            .Build();
        var updatesContext = _updatesContextFactory.GetUpdatesContext(authKeyId,
            ownerUserId);
        int seq = await updatesContext.IncrementSeq();
        return Ferrite.TL.baseLayer.Updates.Builder()
            .Users(ToUserVector(users))
            .UpdatesProperty(ToUpdateVector(new List<TLUpdate> { update }))
            .Chats(new Vector())
            .Seq(seq)
            .Date((int)DateTimeOffset.Now.ToUnixTimeSeconds())
            .Build();
    }

    protected static long? GetInputUserId(Span<byte> inputUserBytes,
        long selfUserId)
    {
        var user = (InputUserView)inputUserBytes;
        if (user.Is(out InputUser inputUser)) return inputUser.UserId;
        if (user.Is(out InputUserFromMessage fromMessage)) return fromMessage.UserId;
        return user.Constructor == Constructors.baseLayer_InputUserSelf
            ? selfUserId
            : null;
    }

    protected static TLUpdates UserIdInvalidUpdates() =>
        (TLUpdates)RpcErrorGenerator.GenerateError(400, "USER_ID_INVALID"u8);

    protected static TLResolvedPeer AuthKeyInvalidResolvedPeer() =>
        (TLResolvedPeer)RpcErrorGenerator.GenerateError(400, "AUTH_KEY_INVALID"u8);

    protected static TLResolvedPeer PhoneNotOccupiedResolvedPeer() =>
        (TLResolvedPeer)RpcErrorGenerator.GenerateError(400, "PHONE_NOT_OCCUPIED"u8);
}
