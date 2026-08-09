// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using Ferrite.Crypto;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.Services.Gateway;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.account;
using Ferrite.TL.baseLayer.auth;
using Ferrite.TL.baseLayer.dto;
using xxHash;
using Vector = Ferrite.TL.Vector;

namespace Ferrite.Services.Handlers.AccountMethods;

public abstract class AccountHandlerBase
{
    private readonly IChatRepository _chatRepository;
    private readonly IPrivacyRulesRepository _privacyRulesRepository;
    private readonly IUserRepository _userRepository;

    protected readonly ISearchEngine _search;
    protected readonly IUpdatesService _updates;
    protected readonly IRandomGenerator _random;
    protected readonly IUnitOfWork _unitOfWork;
    protected readonly IVerificationGateway _verificationGateway;
    protected static Regex UsernameRegex = new Regex("(^[a-zA-Z0-9_]{5,32}$)", RegexOptions.Compiled);
    protected const int PhoneCodeTimeout = 60;//seconds
    protected const int OnlineStatusExpiresInSeconds = 60;
    protected AccountHandlerBase(ISearchEngine search, IUpdatesService updates, IRandomGenerator random,
        IUnitOfWork unitOfWork, IChatRepository chatRepository, IPrivacyRulesRepository privacyRulesRepository, IUserRepository userRepository, IVerificationGateway verificationGateway)
    {
        _chatRepository = chatRepository;
        _privacyRulesRepository = privacyRulesRepository;
        _userRepository = userRepository;

        _search = search;
        _updates = updates;
        _random = random;
        _unitOfWork = unitOfWork;
        _verificationGateway = verificationGateway;
    }

    protected static TLDeviceInfo GetDeviceInfoL57(TLBytes q)
    {
        var registerDevice = new RegisterDeviceL57(q.AsSpan());
        return DeviceInfo.Builder()
            .Token(registerDevice.Token)
            .OtherUids(new VectorOfLong())
            .Secret(ReadOnlySpan<byte>.Empty)
            .TokenType(registerDevice.TokenType)
            .AppSandbox(false)
            .NoMuted(false)
            .Build();
    }

    protected static TLDeviceInfo GetDeviceInfo(TLBytes q)
    {
        var registerDevice = new RegisterDevice(q.AsSpan());
        return DeviceInfo.Builder()
            .Token(registerDevice.Token)
            .OtherUids(registerDevice.OtherUids)
            .Secret(registerDevice.Secret)
            .TokenType(registerDevice.TokenType)
            .AppSandbox(registerDevice.AppSandbox)
            .NoMuted(registerDevice.NoMuted)
            .Build();
    }

    protected readonly record struct UnregisterDeviceParameters(int TokenType, string Token, ICollection<long> OtherUserIds);

    protected static UnregisterDeviceParameters GetUnregisterDeviceParameters(TLBytes q)
    {
        var unregister = new UnregisterDevice(q.AsSpan());
        var token = Encoding.UTF8.GetString(unregister.Token);
        var uids = new long[unregister.OtherUids.Count];
        for (int i = 0; i < unregister.OtherUids.Count; i++)
        {
            uids[i] = unregister.OtherUids[i];
        }

        return new UnregisterDeviceParameters(unregister.TokenType, token, uids);
    }

    protected DeviceType GetDeviceType(TLBytes? info)
    {
        DeviceType deviceType = DeviceType.Other;
        var langPack = info != null
            ? Encoding.UTF8.GetString(((AppInfo)info).LangPack).ToLower()
            : "";
        if (langPack.Contains("android"))
        {
            deviceType = DeviceType.Android;
        }
        else if (langPack.Contains("ios"))
        {
            deviceType = DeviceType.iOS;
        }

        return deviceType;
    }

    protected readonly record struct UpdateNotifySettingsParameters(int NotifyPeerType, int PeerType, long PeerId,
        TLPeerNotifySettings PeerNotifySettings) : IDisposable
    {
        public void Dispose()
        {
            PeerNotifySettings.Dispose();
        }
    }

    protected static UpdateNotifySettingsParameters GetUpdateNotifySettingsParameters(DeviceType deviceType, TLBytes q)
    {
        var settings = new AccountUpdateNotifySettings(q.AsSpan());
        (var peerId, InputNotifyPeerType notifyPeerType, InputPeerType peerType) = GetNotifyPeerInfo(settings.Peer);

        var inputSettings = new InputPeerNotifySettings(settings.Settings);
        var settingsBuilder = PeerNotifySettings
            .Builder()
            .Silent(inputSettings.Silent)
            .MuteUntil(inputSettings.MuteUntil)
            .ShowPreviews(inputSettings.ShowPreviews);
        if (inputSettings.Flags[3])
        {
            switch (deviceType)
            {
                case DeviceType.Android:
                    settingsBuilder = settingsBuilder.AndroidSound(inputSettings.Sound);
                    break;
                case DeviceType.iOS:
                    settingsBuilder = settingsBuilder.IosSound(inputSettings.Sound);
                    break;
                default:
                    settingsBuilder = settingsBuilder.OtherSound(inputSettings.Sound);
                    break;
            }
        }

        return new UpdateNotifySettingsParameters((int)notifyPeerType,
            (int)peerType, peerId, settingsBuilder.Build());
    }

    protected static (long peerId, InputNotifyPeerType notifyPeerType, InputPeerType peerType) GetNotifyPeerInfo(
        Span<byte> peer)
    {
        var view = (InputNotifyPeerView)peer;
        long peerId = 0;
        InputNotifyPeerType notifyPeerType = InputNotifyPeerType.Users;
        InputPeerType peerType = InputPeerType.Empty;
        if (view.Is(out InputNotifyChats _))
        {
            notifyPeerType = InputNotifyPeerType.Chats;
        }
        else if (view.Is(out InputNotifyBroadcasts _))
        {
            notifyPeerType = InputNotifyPeerType.Broadcasts;
        }
        else if (view.Is(out InputNotifyPeer notifyPeer))
        {
            notifyPeerType = InputNotifyPeerType.Peer;
            var (inputPeerType, id, _) = GetPeerTypeAndId(notifyPeer.Peer);
            peerType = inputPeerType;
            peerId = id;
        }

        return (peerId, notifyPeerType, peerType);
    }

    protected static (InputPeerType, long, long) GetPeerTypeAndId(Span<byte> bytes)
    {
        InputPeerType inputPeerType = InputPeerType.Empty;
        var view = (InputPeerView)bytes;
        long peerId = 0;
        long accessHash = 0;
        if (view.Is(out InputPeerSelf _))
        {
            inputPeerType = InputPeerType.Self;
        }
        else if (view.Is(out InputPeerChat chat))
        {
            inputPeerType = InputPeerType.Chat;
            peerId = chat.ChatId;
        }
        else if (view.Is(out InputPeerUser user))
        {
            inputPeerType = InputPeerType.User;
            peerId = user.UserId;
            accessHash = user.AccessHash;
        }
        else if (view.Is(out InputPeerChannel channel))
        {
            inputPeerType = InputPeerType.Channel;
            peerId = channel.ChannelId;
            accessHash = channel.AccessHash;
        }
        else if (view.Is(out InputPeerUserFromMessage userFromMessage))
        {
            inputPeerType = InputPeerType.User;
            var peer = (InputPeerUser)userFromMessage.Peer;
            peerId = peer.UserId;
            accessHash = peer.AccessHash;
        }
        else if (view.Is(out InputPeerChannelFromMessage channelFromMessage))
        {
            inputPeerType = InputPeerType.Channel;
            var peerChannel = (InputPeerChannel)channelFromMessage.Peer;
            peerId = peerChannel.ChannelId;
            accessHash = peerChannel.AccessHash;
        }

        return (inputPeerType, peerId, accessHash);
    }

    protected readonly record struct UserInfo(long UserId, string? Username,
        string? FirstName, string? LastName, string Phone);

    protected static UserInfo GetUserInfo(TLBytes u)
    {
        var user = new User(u.AsSpan());
        string? username = user.Username.Length > 0 ? Encoding.UTF8.GetString(user.Username) : null;
        string? firstname = user.FirstName.Length > 0 ? Encoding.UTF8.GetString(user.FirstName) : null;
        string? lastname = user.LastName.Length > 0 ? Encoding.UTF8.GetString(user.LastName) : null;
        string phone = Encoding.UTF8.GetString(user.Phone);
        return new UserInfo(user.Id, username, firstname, lastname, phone);
    }

    protected static TLUser ModifyUser(TLBytes u, string? firstName, string? lastName)
    {
        var user = new User(u.AsSpan()).Clone();
        if (firstName != null) user = user.FirstName(Encoding.UTF8.GetBytes(firstName));
        if (lastName != null) user = user.LastName(Encoding.UTF8.GetBytes(lastName));
        return user.Build();
    }

    protected readonly record struct ReportPeerParameters(int PeerType, long PeerId, TLReportReasonWithMessage Reason): IDisposable
    {
        public void Dispose()
        {
            Reason.Dispose();
        }
    }

    protected static ReportPeerParameters GetReportPeerParameters(TLBytes q)
    {
        var reportPeer = new ReportPeer(q.AsSpan());
        var (type, id, hash) = GetPeerTypeAndId(reportPeer.Peer);
        var reportReason = ReportReasonWithMessage.Builder()
            .ReportReason(reportPeer.Reason)
            .Message(reportPeer.Message)
            .Build();
        return new ReportPeerParameters((int)type, id, reportReason);
    }

    protected ValueTask<bool> IndexUser(UserInfo userInfo) => _search.IndexUser(
        new Data.Search.UserSearchModel(userInfo.UserId, userInfo.Username,
            userInfo.FirstName, userInfo.LastName, userInfo.Phone));

    protected async Task EnqueueUserNameUpdate(UserInfo userInfo)
    {
        var usernames = new Vector();
        if (!string.IsNullOrEmpty(userInfo.Username))
        {
            using var username = Username.Builder()
                .Editable(true)
                .Active(true)
                .UsernameProperty(Encoding.UTF8.GetBytes(userInfo.Username))
                .Build();
            usernames.AppendTLObject(username.ToReadOnlySpan());
        }

        byte[] firstName = userInfo.FirstName != null
            ? Encoding.UTF8.GetBytes(userInfo.FirstName)
            : Array.Empty<byte>();
        byte[] lastName = userInfo.LastName != null
            ? Encoding.UTF8.GetBytes(userInfo.LastName)
            : Array.Empty<byte>();
        TLUpdate update = UpdateUserName.Builder()
            .UserId(userInfo.UserId)
            .FirstName(firstName)
            .LastName(lastName)
            .Usernames(usernames)
            .Build();
        await _updates.EnqueueUpdate(userInfo.UserId, update);
    }

    protected async Task EnqueueUserInvalidationUpdate(long userId)
    {
        TLUpdate update = UpdateUser.Builder()
            .UserId(userId)
            .Build();
        await _updates.EnqueueUpdate(userId, update);
    }

    protected async Task EnqueueUserStatusUpdate(long userId, bool offline, int now)
    {
        using var status = BuildUserStatus(offline, now);
        TLUpdate update = UpdateUserStatus.Builder()
            .UserId(userId)
            .Status(status.AsSpan())
            .Build();
        await _updates.EnqueueUpdate(userId, update);
    }

    protected static TLUserStatus BuildUserStatus(bool offline, int now)
    {
        if (offline)
        {
            return UserStatusOffline.Builder()
                .WasOnline(now)
                .Build();
        }

        return UserStatusOnline.Builder()
            .Expires(now + OnlineStatusExpiresInSeconds)
            .Build();
    }

    protected bool TryPutPrivacyRules(long userId, InputPrivacyKey key, TLBytes q)
    {
        var rules = ((SetPrivacy)q).Rules;
        Vector converted = new();
        int count = rules.Count;
        for (int i = 0; i < count; i++)
        {
            if (!TryAppendPrivacyValue(ref converted, rules.ReadTLObject(), userId))
            {
                return false;
            }
        }

        if (converted.Count == 0)
        {
            // TDLib omits a trailing inputPrivacyValueDisallowAll because an
            // unmatched rule set already means "not allowed". Persist the explicit
            // disallowAll row so an explicitly configured empty rule set stays
            // distinguishable from a never-configured key (which keeps the
            // per-key server default).
            using var disallowAll = PrivacyValueDisallowAll.Builder().Build();
            converted.AppendTLObject(disallowAll.ToReadOnlySpan());
        }

        return _privacyRulesRepository.PutPrivacyRules(userId, key, converted);
    }

    protected async Task EnqueuePrivacyUpdate(long userId, InputPrivacyKey key)
    {
        var savedRules = await _privacyRulesRepository.GetPrivacyRulesAsync(userId, key);
        var rules = new Vector();
        foreach (var rule in savedRules)
        {
            rules.AppendTLObject(rule.AsSpan());
        }

        using var privacyKey = BuildPrivacyKey(key);
        TLUpdate update = UpdatePrivacy.Builder()
            .Key(privacyKey.AsSpan())
            .Rules(rules)
            .Build();
        await _updates.EnqueueUpdate(userId, update);
    }

    protected static TLPrivacyKey BuildPrivacyKey(InputPrivacyKey key) => key switch
    {
        Data.InputPrivacyKey.StatusTimestamp => PrivacyKeyStatusTimestamp.Builder().Build(),
        Data.InputPrivacyKey.ChatInvite => PrivacyKeyChatInvite.Builder().Build(),
        Data.InputPrivacyKey.PhoneCall => PrivacyKeyPhoneCall.Builder().Build(),
        Data.InputPrivacyKey.PhoneP2P => PrivacyKeyPhoneP2P.Builder().Build(),
        Data.InputPrivacyKey.Forwards => PrivacyKeyForwards.Builder().Build(),
        Data.InputPrivacyKey.ProfilePhoto => PrivacyKeyProfilePhoto.Builder().Build(),
        Data.InputPrivacyKey.PhoneNumber => PrivacyKeyPhoneNumber.Builder().Build(),
        Data.InputPrivacyKey.AddedByPhone => PrivacyKeyAddedByPhone.Builder().Build(),
        Data.InputPrivacyKey.VoiceMessages => PrivacyKeyVoiceMessages.Builder().Build(),
        Data.InputPrivacyKey.About => PrivacyKeyAbout.Builder().Build(),
        Data.InputPrivacyKey.Birthday => PrivacyKeyBirthday.Builder().Build(),
        Data.InputPrivacyKey.StarGiftsAutoSave => PrivacyKeyStarGiftsAutoSave.Builder().Build(),
        Data.InputPrivacyKey.NoPaidMessages => PrivacyKeyNoPaidMessages.Builder().Build(),
        _ => throw new ArgumentOutOfRangeException(nameof(key))
    };

    protected async Task<TLPrivacyRules> GetPrivacyRulesInternal(TLBytes auth, InputPrivacyKey key)
    {
        var savedRules = await _privacyRulesRepository.GetPrivacyRulesAsync(((AuthInfo)auth).UserId, key);
        List<TLBytes> users = new();
        foreach (var id in GetUserIds(savedRules))
        {
            if (_userRepository.GetUser(id) is { } user)
            {
                users.Add(user);
            }
        }

        List<TLBytes> chats = new();
        foreach (var id in GetChatIds(savedRules))
        {
            if (await _chatRepository.GetChatAsync(id) is { } chat)
            {
                chats.Add(chat);
            }
        }

        var saved2 = new List<TLBytes>();
        foreach (var r in savedRules)
        {
            saved2.Add(r);
        }

        return PrivacyRules.Builder()
            .Rules(saved2.ToVector())
            .Users(users.ToVector())
            .Chats(chats.ToVector())
            .Build();
    }

    // Appends the persisted privacy-value form into `result` WHILE the builder is
    // still alive. Returning the builder's ReadOnlySpan would dangle: the `using
    // var` returns the pooled buffer before the caller could copy from it, so the
    // append must happen inside each branch. `result` is passed by ref because
    // Vector is a ref struct (otherwise the caller loses the appended length/regrow).
    // Returns false for an input rule that has no supported persisted form.
    protected bool TryAppendPrivacyValue(ref Vector result, Span<byte> inputPrivacyValue, long currentUserId)
    {
        var view = (InputPrivacyRuleView)inputPrivacyValue;
        if (view.Is(out InputPrivacyValueAllowContacts _))
        {
            using var allowContacts = PrivacyValueAllowContacts.Builder().Build();
            result.AppendTLObject(allowContacts.ToReadOnlySpan());
            return true;
        }
        if (view.Is(out InputPrivacyValueAllowAll _))
        {
            using var allowAll = PrivacyValueAllowAll.Builder().Build();
            result.AppendTLObject(allowAll.ToReadOnlySpan());
            return true;
        }
        if (view.Is(out InputPrivacyValueAllowUsers allowUsers))
        {
            var userVector = allowUsers.Users;
            VectorOfLong userIds = new();
            for (int i = 0; i < userVector.Count; i++)
            {
                var user = userVector.ReadTLObject();
                if (!TryGetUserId(user, currentUserId, out var userId))
                {
                    return false;
                }
                userIds.Append(userId);
            }
            using var allowUsers2 = PrivacyValueAllowUsers.Builder().Users(userIds).Build();
            result.AppendTLObject(allowUsers2.ToReadOnlySpan());
            return true;
        }
        if (view.Is(out InputPrivacyValueDisallowContacts _))
        {
            using var disallowContacts = PrivacyValueDisallowContacts.Builder().Build();
            result.AppendTLObject(disallowContacts.ToReadOnlySpan());
            return true;
        }
        if (view.Is(out InputPrivacyValueDisallowAll _))
        {
            using var disallowAll = PrivacyValueDisallowAll.Builder().Build();
            result.AppendTLObject(disallowAll.ToReadOnlySpan());
            return true;
        }
        if (view.Is(out InputPrivacyValueDisallowUsers disallowUsers))
        {
            var userVector2 = disallowUsers.Users;
            VectorOfLong userIds2 = new();
            for (int i = 0; i < userVector2.Count; i++)
            {
                var user = userVector2.ReadTLObject();
                if (!TryGetUserId(user, currentUserId, out var userId))
                {
                    return false;
                }
                userIds2.Append(userId);
            }
            using var disallowUsers2 = PrivacyValueDisallowUsers.Builder().Users(userIds2).Build();
            result.AppendTLObject(disallowUsers2.ToReadOnlySpan());
            return true;
        }
        if (view.Is(out InputPrivacyValueAllowChatParticipants allowChats))
        {
            var chats = allowChats.Chats;
            using var allowChatParticipants = PrivacyValueAllowChatParticipants.Builder().Chats(chats).Build();
            result.AppendTLObject(allowChatParticipants.ToReadOnlySpan());
            return true;
        }
        if (view.Is(out InputPrivacyValueDisallowChatParticipants disallowChats))
        {
            var chats2 = disallowChats.Chats;
            using var disallowChatParticipants = PrivacyValueDisallowChatParticipants.Builder().Chats(chats2).Build();
            result.AppendTLObject(disallowChatParticipants.ToReadOnlySpan());
            return true;
        }
        if (view.Is(out InputPrivacyValueAllowCloseFriends _))
        {
            using var allowCloseFriends = PrivacyValueAllowCloseFriends.Builder().Build();
            result.AppendTLObject(allowCloseFriends.ToReadOnlySpan());
            return true;
        }
        if (view.Is(out InputPrivacyValueAllowPremium _))
        {
            using var allowPremium = PrivacyValueAllowPremium.Builder().Build();
            result.AppendTLObject(allowPremium.ToReadOnlySpan());
            return true;
        }
        if (view.Is(out InputPrivacyValueAllowBots _))
        {
            using var allowBots = PrivacyValueAllowBots.Builder().Build();
            result.AppendTLObject(allowBots.ToReadOnlySpan());
            return true;
        }
        if (view.Is(out InputPrivacyValueDisallowBots _))
        {
            using var disallowBots = PrivacyValueDisallowBots.Builder().Build();
            result.AppendTLObject(disallowBots.ToReadOnlySpan());
            return true;
        }
        return false;
    }

    protected ICollection<long> GetUserIds(ICollection<TLPrivacyRule> rules)
    {
        List<long> users = new();
        foreach (var r in rules)
        {
            switch (r.Constructor)
            {
                case Constructors.baseLayer_PrivacyValueAllowUsers:
                    var v = r.AsPrivacyValueAllowUsers().Users;
                    for (int i = 0; i < v.Count; i++)
                    {
                        users.Add(v[i]);
                    }
                    break;
                case Constructors.baseLayer_PrivacyValueDisallowUsers:
                    var v2 = r.AsPrivacyValueDisallowUsers().Users;
                    for (int i = 0; i < v2.Count; i++)
                    {
                        users.Add(v2[i]);
                    }
                    break;
            }
        }

        return users;
    }

    protected ICollection<long> GetChatIds(ICollection<TLPrivacyRule> rules)
    {
        List<long> chats = new();
        foreach (var r in rules)
        {
            switch (r.Constructor)
            {
                case Constructors.baseLayer_PrivacyValueAllowChatParticipants:
                    var v = r.AsPrivacyValueAllowChatParticipants().Chats;
                    for (int i = 0; i < v.Count; i++)
                    {
                        chats.Add(v[i]);
                    }
                    break;
                case Constructors.baseLayer_PrivacyValueDisallowChatParticipants:
                    var v2 = r.AsPrivacyValueDisallowChatParticipants().Chats;
                    for (int i = 0; i < v2.Count; i++)
                    {
                        chats.Add(v2[i]);
                    }
                    break;
            }
        }

        return chats;
    }

    protected static bool TryGetUserId(Span<byte> inputUser, long currentUserId, out long userId)
    {
        var view = (InputUserView)inputUser;
        if (view.Is(out InputUser user))
        {
            userId = user.UserId;
            return true;
        }
        if (view.Is(out InputUserSelf _))
        {
            userId = currentUserId;
            return true;
        }
        if (view.Is(out InputUserFromMessage fromMessage))
        {
            userId = fromMessage.UserId;
            return true;
        }

        userId = 0;
        return false;
    }

    protected static InputPrivacyKey? GetPrivacyKey(Span<byte> inputPrivacyKey)
    {
        return GetPrivacyKey(((InputPrivacyKeyView)inputPrivacyKey).Constructor);
    }

    protected static InputPrivacyKey? GetPrivacyKey(int constructor) => constructor switch
    {
        Constructors.baseLayer_InputPrivacyKeyStatusTimestamp => Data.InputPrivacyKey.StatusTimestamp,
        Constructors.baseLayer_InputPrivacyKeyChatInvite => Data.InputPrivacyKey.ChatInvite,
        Constructors.baseLayer_InputPrivacyKeyPhoneCall => Data.InputPrivacyKey.PhoneCall,
        Constructors.baseLayer_InputPrivacyKeyPhoneP2P => Data.InputPrivacyKey.PhoneP2P,
        Constructors.baseLayer_InputPrivacyKeyForwards => Data.InputPrivacyKey.Forwards,
        Constructors.baseLayer_InputPrivacyKeyProfilePhoto => Data.InputPrivacyKey.ProfilePhoto,
        Constructors.baseLayer_InputPrivacyKeyPhoneNumber => Data.InputPrivacyKey.PhoneNumber,
        Constructors.baseLayer_InputPrivacyKeyAddedByPhone => Data.InputPrivacyKey.AddedByPhone,
        Constructors.baseLayer_InputPrivacyKeyVoiceMessages => Data.InputPrivacyKey.VoiceMessages,
        Constructors.baseLayer_InputPrivacyKeyAbout => Data.InputPrivacyKey.About,
        Constructors.baseLayer_InputPrivacyKeyBirthday => Data.InputPrivacyKey.Birthday,
        Constructors.baseLayer_InputPrivacyKeyStarGiftsAutoSave => Data.InputPrivacyKey.StarGiftsAutoSave,
        Constructors.baseLayer_InputPrivacyKeyNoPaidMessages => Data.InputPrivacyKey.NoPaidMessages,
        _ => null
    };

    protected static TLSentCode GenerateSentCode(string hash)
    {
        using var codeType = SentCodeTypeSms.Builder().Build();
        TLSentCode sentCode = SentCode.Builder()
            .Type(codeType.ToReadOnlySpan())
            .PhoneCodeHash(Encoding.UTF8.GetBytes(hash))
            .Timeout(PhoneCodeTimeout)
            .Build();
        return sentCode;
    }

    protected string GeneratePhoneCodeHash(string code)
    {
        var codeBytes = Encoding.UTF8.GetBytes(code);
        return codeBytes.GetXxHash64(1071).ToString("x");
    }

    protected TLAuthorizations GenerateAuthorizations(int ttl,
        long currentAuthKeyId, List<TLAppInfo> infos)
    {
        Vector authVector = new();
        foreach (var info in infos)
        {
            var a = info.AsAppInfo();
            using var auth = Authorization.Builder()
                .Current(a.AuthKeyId == currentAuthKeyId)
                .Hash(a.Hash)
                .DeviceModel(a.DeviceModel)
                .Platform("Unknown"u8)
                .SystemVersion(a.SystemVersion)
                .ApiId(a.ApiId)
                .AppName("Unknown"u8)
                .AppVersion(a.AppVersion)
                .DateCreated((int)DateTimeOffset.Now.ToUnixTimeSeconds())
                .DateActive((int)DateTimeOffset.Now.ToUnixTimeSeconds())
                .Ip(a.Ip)
                .Country("Turkey"u8)
                .Region("Unknown"u8)
                .Build();
            authVector.AppendTLObject(auth.ToReadOnlySpan());
        }
        return Authorizations.Builder()
            .AuthorizationTtlDays(ttl)
            .AuthorizationsProperty(authVector)
            .Build();
    }

}
