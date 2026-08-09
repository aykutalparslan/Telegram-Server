// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Crypto;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.account;
using Ferrite.TL.baseLayer.contacts;
using Ferrite.TL.baseLayer.dto;
using Vector = Ferrite.TL.Vector;

namespace Ferrite.Services;

public sealed class ProfileStore
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IContactsRepository _contactsRepository;
    private readonly IUserRepository _userRepository;

    private const int MaxCloseFriends = 100;
    private readonly IAccountSettingsRepository _profiles;
    private readonly IUnitOfWork _transactions;
    private readonly IUpdatesService _updates;
    private readonly IRandomGenerator _random;
    private readonly TimeProvider _time;

    public ProfileStore(IAccountSettingsRepository profiles,
        IUnitOfWork transactions, IAuthorizationRepository authorizationRepository, IContactsRepository contactsRepository, IUserRepository userRepository, IUpdatesService updates,
        IRandomGenerator random, TimeProvider time)
    {
        _authorizationRepository = authorizationRepository;
        _contactsRepository = contactsRepository;
        _userRepository = userRepository;

        _profiles = profiles;
        _transactions = transactions;
        _updates = updates;
        _random = random;
        _time = time;
    }

    public async ValueTask<long?> GetUserIdAsync(long authKeyId)
    {
        using TLAuthInfo? auth = await _authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        return auth is not null && auth.Value.AsAuthInfo().LoggedIn
            ? auth.Value.AsAuthInfo().UserId : null;
    }

    public ValueTask<TLAccountProfileState?> GetProfileAsync(long userId) =>
        _profiles.GetProfileAsync(userId);

    public async ValueTask<TLUser> HydrateUserAsync(long viewerUserId,
        long sourceUserId, TLUser source)
    {
        using TLAccountProfileState? stored = await _profiles
            .GetProfileAsync(sourceUserId);
        bool closeFriend = await _profiles.IsCloseFriendAsync(viewerUserId,
            sourceUserId);
        User value = source.AsUser();
        var builder = value.Clone().CloseFriend(closeFriend);
        if (stored is not null)
        {
            AccountProfileState profile = stored.Value.AsAccountProfileState();
            if (profile.Flags[0])
            {
                var usernames = new Vector();
                CopyVector(ref usernames, profile.Usernames);
                builder = builder.Usernames(usernames);
            }
            if (profile.Flags[1]) builder = builder.Color(profile.Color);
            if (profile.Flags[2])
                builder = builder.ProfileColor(profile.ProfileColor);
            if (profile.Flags[3])
                builder = builder.EmojiStatus(profile.EmojiStatus);
        }
        return builder.Build();
    }

    public async Task<TLBool> ToggleUsernameAsync(long userId, string username,
        bool active)
    {
        List<UsernameState> usernames = await ReadUsernamesAsync(userId);
        int index = usernames.FindIndex(x => string.Equals(x.Username, username,
            StringComparison.OrdinalIgnoreCase));
        if (index < 0) return Error("USERNAME_NOT_OCCUPIED");
        UsernameState current = usernames[index];
        usernames[index] = current with { Active = active };
        return await SaveUsernamesAsync(userId, usernames);
    }

    public async Task<TLBool> ReorderUsernamesAsync(long userId,
        IReadOnlyList<string> order)
    {
        if (order.Count != order.Distinct(StringComparer.OrdinalIgnoreCase).Count())
            return Error("ORDER_INVALID");
        List<UsernameState> current = await ReadUsernamesAsync(userId);
        List<UsernameState> active = current.Where(x => x.Active).ToList();
        if (active.Count != order.Count || order.Any(name =>
                active.All(x => !string.Equals(x.Username, name,
                    StringComparison.OrdinalIgnoreCase))))
        {
            return Error("ORDER_INVALID");
        }
        var reordered = new List<UsernameState>(current.Count);
        foreach (string name in order)
        {
            reordered.Add(active.Single(x => string.Equals(x.Username, name,
                StringComparison.OrdinalIgnoreCase)));
        }
        reordered.AddRange(current.Where(x => !x.Active));
        return await SaveUsernamesAsync(userId, reordered);
    }

    public async Task<bool> SyncPrimaryUsernameAsync(long userId,
        string username)
    {
        using TLAccountProfileState? stored = await _profiles
            .GetProfileAsync(userId);
        if (stored is null) return true;
        List<UsernameState> usernames = await ReadUsernamesAsync(userId);
        int editable = usernames.FindIndex(x => x.Editable);
        if (string.IsNullOrEmpty(username))
        {
            if (editable >= 0) usernames.RemoveAt(editable);
        }
        else
        {
            int matching = usernames.FindIndex(x => string.Equals(x.Username,
                username, StringComparison.OrdinalIgnoreCase));
            if (matching >= 0)
            {
                UsernameState value = usernames[matching];
                usernames[matching] = value with { Editable = true, Active = true };
                if (editable >= 0 && editable != matching)
                    usernames.RemoveAt(editable);
            }
            else if (editable >= 0)
            {
                usernames[editable] = new UsernameState(username, true, true);
            }
            else
            {
                usernames.Insert(0, new UsernameState(username, true, true));
            }
        }
        return await MutateProfileAsync(userId, replaceUsernames: true,
            usernames: usernames, emitUpdate: false);
    }

    public async Task<TLBool> UpdateColorAsync(long userId, bool forProfile,
        TLPeerColor? color)
    {
        bool saved = forProfile
            ? await MutateProfileAsync(userId, replaceProfileColor: true,
                profileColor: color)
            : await MutateProfileAsync(userId, replaceColor: true, color: color);
        return saved ? BoolTrue.Builder().Build() : InternalError();
    }

    public async Task<TLBool> UpdateEmojiStatusAsync(long userId,
        TLEmojiStatus status)
    {
        if (!await MutateProfileAsync(userId, replaceEmoji: true, emoji: status,
                replaceRecentEmojiStatuses: true, emitUpdate: false))
            return InternalError();
        await EnqueueEmojiStatusAsync(userId, status);
        return BoolTrue.Builder().Build();
    }

    public async Task<TLBytes> GetRecentEmojiStatusesAsync(long userId,
        long requestedHash)
    {
        using TLAccountProfileState? stored = await _profiles
            .GetProfileAsync(userId);
        var statuses = new Vector();
        long hash = 1;
        if (stored is not null &&
            stored.Value.AsAccountProfileState().Flags[7])
        {
            Vector source = stored.Value.AsAccountProfileState()
                .RecentEmojiStatuses;
            for (int i = 0; i < source.Count; i++)
            {
                Span<byte> bytes = source.ReadTLObject();
                EmojiStatusKey key = GetEmojiStatusKey(
                    (EmojiStatusView)bytes);
                hash = unchecked(hash * 20261 + key.Id * 31 + key.Constructor);
                statuses.AppendTLObject(bytes);
            }
        }
        if (requestedHash != 0 && requestedHash == hash)
            return EmojiStatusesNotModified.Builder().Build().TLBytes!.Value;
        return EmojiStatuses.Builder().Hash(hash).Statuses(statuses).Build()
            .TLBytes!.Value;
    }

    public async Task<TLBool> ClearRecentEmojiStatusesAsync(long userId)
    {
        if (!await MutateProfileAsync(userId,
                replaceRecentEmojiStatuses: true,
                clearRecentEmojiStatuses: true, emitUpdate: false))
            return InternalError();
        using TLUpdate update = UpdateRecentEmojiStatuses.Builder().Build();
        await _updates.EnqueueUpdate(userId, update);
        return BoolTrue.Builder().Build();
    }

    public async Task<TLBool> UpdateBirthdayAsync(long userId,
        TLBirthday? birthday) =>
        await MutateProfileAsync(userId, replaceBirthday: true,
            birthday: birthday)
            ? BoolTrue.Builder().Build() : InternalError();

    public async Task<TLBool> UpdatePersonalChannelAsync(long userId,
        long? channelId) =>
        await MutateProfileAsync(userId, replacePersonalChannel: true,
            personalChannelId: channelId)
            ? BoolTrue.Builder().Build() : InternalError();

    public async Task<TLBool> SetMainProfileTabAsync(long userId,
        TLProfileTab tab) =>
        await MutateProfileAsync(userId, replaceMainTab: true, mainTab: tab)
            ? BoolTrue.Builder().Build() : InternalError();

    public async Task<TLBool> ReplaceCloseFriendsAsync(long userId,
        IReadOnlyCollection<long> ids)
    {
        if (ids.Count > MaxCloseFriends || ids.Count != ids.Distinct().Count())
            return Error("USER_ID_INVALID");
        foreach (long id in ids)
        {
            if (id == userId || _userRepository.GetUser(id) is not
                { } user) return Error("USER_ID_INVALID");
            user.Dispose();
        }

        IReadOnlyCollection<TLCloseFriendState> previous = await _profiles
            .GetCloseFriendsAsync(userId);
        var changedIds = new HashSet<long>(ids);
        foreach (TLCloseFriendState row in previous)
        {
            using (row)
                changedIds.Add(row.AsCloseFriendState().CloseFriendId);
        }
        bool success = _profiles.DeleteCloseFriends(userId);
        int now = Now();
        foreach (long id in ids)
        {
            using TLCloseFriendState row = CloseFriendState.Builder()
                .UserId(userId).CloseFriendId(id).Date(now).Build();
            success &= _profiles.PutCloseFriend(row);
        }
        if (!success || !await _transactions.SaveAsync()) return InternalError();
        foreach (long id in changedIds)
            await EnqueueUserInvalidationAsync(id, userId);
        return BoolTrue.Builder().Build();
    }

    public async Task<TLBytes> GetBirthdaysAsync(long userId)
    {
        IReadOnlyList<TLContact> contacts = _contactsRepository
            .GetContacts(userId);
        var birthdays = new List<TLContactBirthday>();
        var users = new List<TLUser>();
        try
        {
            foreach (TLContact contact in contacts)
            {
                long contactId = contact.AsContact().UserId;
                using TLAccountProfileState? stored = await _profiles
                    .GetProfileAsync(contactId);
                if (stored is null ||
                    !stored.Value.AsAccountProfileState().Flags[4]) continue;
                using TLUser? source = _userRepository
                    .GetUser(contactId);
                if (source is null) continue;
                TLUser hydrated = await HydrateUserAsync(userId, contactId,
                    source.Value);
                TLContactBirthday item = ContactBirthday.Builder()
                    .ContactId(contactId)
                    .Birthday(stored.Value.AsAccountProfileState().Birthday)
                    .Build();
                birthdays.Add(item);
                users.Add(hydrated);
            }
            var birthdayVector = new Vector();
            var userVector = new Vector();
            foreach (TLContactBirthday item in birthdays)
                birthdayVector.AppendTLObject(item.AsSpan());
            foreach (TLUser user in users)
                userVector.AppendTLObject(user.AsSpan());
            return ContactBirthdays.Builder().Contacts(birthdayVector)
                .Users(userVector).Build().TLBytes!.Value;
        }
        finally
        {
            foreach (TLContact contact in contacts) contact.Dispose();
            foreach (TLContactBirthday birthday in birthdays) birthday.Dispose();
            foreach (TLUser user in users) user.Dispose();
        }
    }

    public TLBytes GetRequirementsToContact(long userId,
        IReadOnlyList<long> targetIds)
    {
        var result = new Vector();
        foreach (long targetId in targetIds)
        {
            using TLUser? target = _userRepository.GetUser(targetId);
            if (target is null) return ErrorBytes("USER_ID_INVALID");
            User user = target.Value.AsUser();
            if (_contactsRepository.HasContact(userId, targetId))
            {
                using RequirementToContactEmpty item =
                    RequirementToContactEmpty.Builder().Build();
                result.AppendTLObject(item.ToReadOnlySpan());
            }
            else if (user.Flags2[15])
            {
                using RequirementToContactPaidMessages item =
                    RequirementToContactPaidMessages.Builder()
                        .StarsAmount(user.SendPaidMessagesStars).Build();
                result.AppendTLObject(item.ToReadOnlySpan());
            }
            else if (user.ContactRequirePremium)
            {
                using RequirementToContactPremium item =
                    RequirementToContactPremium.Builder().Build();
                result.AppendTLObject(item.ToReadOnlySpan());
            }
            else
            {
                using RequirementToContactEmpty item =
                    RequirementToContactEmpty.Builder().Build();
                result.AppendTLObject(item.ToReadOnlySpan());
            }
        }
        byte[] bytes = result.ToReadOnlySpan().ToArray();
        return new TLBytes(bytes, 0, bytes.Length);
    }

    public async Task<TLBytes> ExportContactTokenAsync(long userId)
    {
        IReadOnlyCollection<TLContactTokenState> old = await _profiles
            .GetContactTokensAsync(userId);
        foreach (TLContactTokenState state in old)
        {
            using (state)
            {
                ContactTokenState row = state.AsContactTokenState();
                _profiles.DeleteContactToken(userId,
                    Encoding.UTF8.GetString(row.Token));
            }
        }
        int expires = Now() + 24 * 60 * 60;
        string token = $"{userId:x}-{(_random.NextLong() & long.MaxValue):x}-{expires:x}";
        using TLContactTokenState next = ContactTokenState.Builder()
            .Token(Encoding.UTF8.GetBytes(token)).UserId(userId)
            .ExpiresAt(expires).Build();
        if (!_profiles.PutContactToken(next) ||
            !await _transactions.SaveAsync()) return InternalBytes();
        return ExportedContactToken.Builder()
            .Url(Encoding.UTF8.GetBytes($"https://t.me/contact/{token}"))
            .Expires(expires).Build().TLBytes!.Value;
    }

    public async Task<TLBytes> ImportContactTokenAsync(long userId,
        string rawToken)
    {
        string token = rawToken.Trim().TrimEnd('/');
        int slash = token.LastIndexOf('/');
        if (slash >= 0) token = token[(slash + 1)..];
        using TLContactTokenState? stored = await _profiles
            .GetContactTokenAsync(token);
        if (stored is null || stored.Value.AsContactTokenState().ExpiresAt <=
            Now()) return ErrorBytes("TOKEN_INVALID");
        long targetId = stored.Value.AsContactTokenState().UserId;
        using TLUser? target = _userRepository.GetUser(targetId);
        using TLUser? importer = _userRepository.GetUser(userId);
        if (target is null || importer is null) return ErrorBytes("USER_ID_INVALID");

        bool success = true;
        if (targetId != userId)
        {
            using TLContactInfo targetInfo = ContactInfo(target.Value, 0);
            using TLContactInfo importerInfo = ContactInfo(importer.Value, 0);
            using TLImportedContact first = _contactsRepository
                .PutContact(userId, targetId, targetInfo);
            using TLImportedContact second = _contactsRepository
                .PutContact(targetId, userId, importerInfo);
            success = await _transactions.SaveAsync();
        }
        if (!success) return InternalBytes();
        using TLUser hydrated = await HydrateUserAsync(userId, targetId,
            target.Value);
        return hydrated.AsUser().Clone().Contact(targetId != userId).Build()
            .TLBytes!.Value;
    }

    private async Task<List<UsernameState>> ReadUsernamesAsync(long userId)
    {
        using TLAccountProfileState? stored = await _profiles
            .GetProfileAsync(userId);
        var result = new List<UsernameState>();
        if (stored is not null && stored.Value.AsAccountProfileState().Flags[0])
        {
            Vector vector = stored.Value.AsAccountProfileState().Usernames;
            for (int i = 0; i < vector.Count; i++)
            {
                var username = new Username(vector.ReadTLObject());
                result.Add(new UsernameState(
                    Encoding.UTF8.GetString(username.UsernameProperty),
                    username.Editable, username.Active));
            }
            return result;
        }
        using TLUser? user = _userRepository.GetUser(userId);
        if (user is not null && user.Value.AsUser().Username.Length > 0)
        {
            result.Add(new UsernameState(Encoding.UTF8.GetString(
                user.Value.AsUser().Username), true, true));
        }
        return result;
    }

    private async Task<TLBool> SaveUsernamesAsync(long userId,
        IReadOnlyList<UsernameState> usernames) =>
        await MutateProfileAsync(userId, replaceUsernames: true,
            usernames: usernames)
            ? BoolTrue.Builder().Build() : InternalError();

    private async Task<bool> MutateProfileAsync(long userId,
        bool replaceUsernames = false,
        IReadOnlyList<UsernameState>? usernames = null,
        bool replaceColor = false, TLPeerColor? color = null,
        bool replaceProfileColor = false, TLPeerColor? profileColor = null,
        bool replaceEmoji = false, TLEmojiStatus? emoji = null,
        bool replaceBirthday = false, TLBirthday? birthday = null,
        bool replacePersonalChannel = false, long? personalChannelId = null,
        bool replaceMainTab = false, TLProfileTab? mainTab = null,
        bool replaceRecentEmojiStatuses = false,
        bool clearRecentEmojiStatuses = false,
        bool emitUpdate = true)
    {
        using TLAccountProfileState? stored = await _profiles
            .GetProfileAsync(userId);
        var builder = AccountProfileState.Builder().UserId(userId).Date(Now());
        Vector usernameVector = new();
        if (replaceUsernames)
        {
            foreach (UsernameState item in usernames ?? [])
            {
                using Username value = Username.Builder().Editable(item.Editable)
                    .Active(item.Active)
                    .UsernameProperty(Encoding.UTF8.GetBytes(item.Username))
                    .Build();
                usernameVector.AppendTLObject(value.ToReadOnlySpan());
            }
            if (usernameVector.Count > 0)
                builder = builder.Usernames(usernameVector);
        }
        else if (stored is not null &&
                 stored.Value.AsAccountProfileState().Flags[0])
        {
            CopyVector(ref usernameVector,
                stored.Value.AsAccountProfileState().Usernames);
            builder = builder.Usernames(usernameVector);
        }

        AccountProfileState existing = stored is null
            ? default : stored.Value.AsAccountProfileState();
        if (replaceColor)
        {
            if (color is not null) builder = builder.Color(color.Value.AsSpan());
        }
        else if (stored is not null && existing.Flags[1])
            builder = builder.Color(existing.Color);
        if (replaceProfileColor)
        {
            if (profileColor is not null)
                builder = builder.ProfileColor(profileColor.Value.AsSpan());
        }
        else if (stored is not null && existing.Flags[2])
            builder = builder.ProfileColor(existing.ProfileColor);
        if (replaceEmoji)
        {
            if (emoji is not null)
                builder = builder.EmojiStatus(emoji.Value.AsSpan());
        }
        else if (stored is not null && existing.Flags[3])
            builder = builder.EmojiStatus(existing.EmojiStatus);
        if (replaceBirthday)
        {
            if (birthday is not null)
                builder = builder.Birthday(birthday.Value.AsSpan());
        }
        else if (stored is not null && existing.Flags[4])
            builder = builder.Birthday(existing.Birthday);
        if (replacePersonalChannel)
        {
            if (personalChannelId is not null)
                builder = builder.PersonalChannelId(personalChannelId.Value);
        }
        else if (stored is not null && existing.Flags[5])
            builder = builder.PersonalChannelId(existing.PersonalChannelId);
        if (replaceMainTab)
        {
            if (mainTab is not null)
                builder = builder.MainTab(mainTab.Value.AsSpan());
        }
        else if (stored is not null && existing.Flags[6])
            builder = builder.MainTab(existing.MainTab);
        var recentEmojiStatuses = new Vector();
        if (replaceRecentEmojiStatuses)
        {
            var seen = new HashSet<EmojiStatusKey>();
            if (emoji is not null && emoji.Value.Constructor !=
                Constructors.baseLayer_EmojiStatusEmpty)
            {
                seen.Add(GetEmojiStatusKey(
                    (EmojiStatusView)emoji.Value.AsSpan()));
                recentEmojiStatuses.AppendTLObject(emoji.Value.AsSpan());
            }
            if (!clearRecentEmojiStatuses && stored is not null &&
                existing.Flags[7])
            {
                Vector source = existing.RecentEmojiStatuses;
                for (int i = 0; i < source.Count &&
                    recentEmojiStatuses.Count < 20; i++)
                {
                    Span<byte> bytes = source.ReadTLObject();
                    if (seen.Add(GetEmojiStatusKey((EmojiStatusView)bytes)))
                        recentEmojiStatuses.AppendTLObject(bytes);
                }
            }
            if (recentEmojiStatuses.Count > 0)
                builder = builder.RecentEmojiStatuses(recentEmojiStatuses);
        }
        else if (stored is not null && existing.Flags[7])
        {
            CopyVector(ref recentEmojiStatuses, existing.RecentEmojiStatuses);
            builder = builder.RecentEmojiStatuses(recentEmojiStatuses);
        }

        using TLAccountProfileState next = builder.Build();
        if (!_profiles.PutProfile(next) || !await _transactions.SaveAsync())
            return false;
        if (emitUpdate) await EnqueueUserInvalidationAsync(userId, userId);
        return true;
    }

    private async Task EnqueueUserInvalidationAsync(long changedUserId,
        long ownerUserId)
    {
        var recipients = _contactsRepository
            .GetContactOwners(changedUserId).Append(ownerUserId).Distinct();
        foreach (long recipient in recipients)
        {
            TLUpdate update = UpdateUser.Builder().UserId(changedUserId).Build();
            await _updates.EnqueueUpdate(recipient, update);
        }
    }

    private async Task EnqueueEmojiStatusAsync(long userId,
        TLEmojiStatus status)
    {
        var recipients = _contactsRepository.GetContactOwners(userId)
            .Append(userId).Distinct();
        foreach (long recipient in recipients)
        {
            using TLUpdate update = UpdateUserEmojiStatus.Builder()
                .UserId(userId).EmojiStatus(status.AsSpan()).Build();
            await _updates.EnqueueUpdate(recipient, update);
        }
    }

    private static EmojiStatusKey GetEmojiStatusKey(EmojiStatusView status)
    {
        if (status.Is(out EmojiStatus value))
            return new EmojiStatusKey(status.Constructor, value.DocumentId);
        if (status.Is(out EmojiStatusCollectible collectible))
            return new EmojiStatusKey(status.Constructor,
                collectible.CollectibleId);
        return new EmojiStatusKey(status.Constructor, 0);
    }

    private static void CopyVector(ref Vector result, Vector source)
    {
        for (int i = 0; i < source.Count; i++)
            result.AppendTLObject(source.ReadTLObject());
    }

    private TLContactInfo ContactInfo(TLUser value, long clientId)
    {
        User user = value.AsUser();
        return Ferrite.TL.baseLayer.dto.ContactInfo.Builder()
            .Phone(user.Phone).FirstName(user.FirstName).LastName(user.LastName)
            .ClientId(clientId).UserId(user.Id).Date(Now()).Build();
    }

    private int Now() => (int)_time.GetUtcNow().ToUnixTimeSeconds();

    private static TLBool Error(string message) =>
        (TLBool)ErrorBytes(message);

    private static TLBytes ErrorBytes(string message) =>
        RpcErrorGenerator.GenerateError(400, Encoding.UTF8.GetBytes(message));

    private static TLBool InternalError() =>
        (TLBool)InternalBytes();

    private static TLBytes InternalBytes() =>
        RpcErrorGenerator.GenerateError(500, "INTERNAL_SERVER_ERROR"u8);

    private readonly record struct UsernameState(string Username, bool Editable,
        bool Active);
    private readonly record struct EmojiStatusKey(int Constructor, long Id);
}
