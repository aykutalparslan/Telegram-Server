// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Crypto;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.account;
using Ferrite.TL.baseLayer.dto;
using Vector = Ferrite.TL.Vector;
using TLDownloadPreset = Ferrite.TL.baseLayer.TLAutoDownloadSettings;
using TLSavePreset = Ferrite.TL.baseLayer.TLAutoSaveSettings;

namespace Ferrite.Services;

public sealed class AccountSettingsStore
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IChatRepository _chatRepository;
    private readonly IUserRepository _userRepository;

    public const int AutoSaveUsers = 0;
    public const int AutoSaveChats = 1;
    public const int AutoSaveBroadcasts = 2;
    public const int AutoSavePeer = 3;
    public const int DefaultAuthorizationTtlDays = 180;

    private readonly IAccountSettingsRepository _repository;
    private readonly IUnitOfWork _transactions;
    private readonly IRandomGenerator _random;
    private readonly TimeProvider _time;

    public AccountSettingsStore(IAccountSettingsRepository repository,
        IUnitOfWork transactions, IAuthorizationRepository authorizationRepository, IChatRepository chatRepository, IUserRepository userRepository, IRandomGenerator random, TimeProvider time)
    {
        _authorizationRepository = authorizationRepository;
        _chatRepository = chatRepository;
        _userRepository = userRepository;

        _repository = repository;
        _transactions = transactions;
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

    public async Task<TLBytes> GetAutoDownloadAsync(long userId)
    {
        using TLAutoDownloadSettingsState? stored = await _repository
            .GetAutoDownloadSettingsAsync(userId);
        if (stored is not null)
        {
            var row = stored.Value.AsAutoDownloadSettingsState();
            return AccountAutoDownloadSettings.Builder().Low(row.Low)
                .Medium(row.Medium).High(row.High).Build().TLBytes!.Value;
        }

        using TLDownloadPreset low = BuildDownloadPreset(512 * 1024,
            2 * 1024 * 1024, 1024 * 1024, disabled: false);
        using TLDownloadPreset medium = BuildDownloadPreset(1024 * 1024,
            10 * 1024 * 1024, 5 * 1024 * 1024, disabled: false);
        using TLDownloadPreset high = BuildDownloadPreset(2 * 1024 * 1024,
            20 * 1024 * 1024, 10 * 1024 * 1024, disabled: false);
        return AccountAutoDownloadSettings.Builder().Low(low.AsSpan())
            .Medium(medium.AsSpan()).High(high.AsSpan()).Build().TLBytes!.Value;
    }

    public async Task<TLBool> SaveAutoDownloadAsync(long userId, bool low,
        bool high, TLDownloadPreset settings)
    {
        if (low && high)
        {
            return BoolError(400, "SETTINGS_INVALID"u8);
        }

        using TLAutoDownloadSettingsState? stored = await _repository
            .GetAutoDownloadSettingsAsync(userId);
        using TLDownloadPreset defaultLow = BuildDownloadPreset(512 * 1024,
            2 * 1024 * 1024, 1024 * 1024, disabled: false);
        using TLDownloadPreset defaultMedium = BuildDownloadPreset(
            1024 * 1024, 10 * 1024 * 1024, 5 * 1024 * 1024,
            disabled: false);
        using TLDownloadPreset defaultHigh = BuildDownloadPreset(
            2 * 1024 * 1024, 20 * 1024 * 1024, 10 * 1024 * 1024,
            disabled: false);

        ReadOnlySpan<byte> currentLow = stored is null
            ? defaultLow.AsSpan() : stored.Value.AsAutoDownloadSettingsState().Low;
        ReadOnlySpan<byte> currentMedium = stored is null
            ? defaultMedium.AsSpan() : stored.Value.AsAutoDownloadSettingsState().Medium;
        ReadOnlySpan<byte> currentHigh = stored is null
            ? defaultHigh.AsSpan() : stored.Value.AsAutoDownloadSettingsState().High;
        using TLAutoDownloadSettingsState next = AutoDownloadSettingsState.Builder()
            .UserId(userId)
            .Low(low ? settings.AsSpan() : currentLow)
            .Medium(!low && !high ? settings.AsSpan() : currentMedium)
            .High(high ? settings.AsSpan() : currentHigh)
            .Date(Now()).Build();
        if (!_repository.PutAutoDownloadSettings(next) ||
            !await _transactions.SaveAsync())
        {
            return BoolError(500, "INTERNAL_SERVER_ERROR"u8);
        }
        return BoolTrue.Builder().Build();
    }

    public async Task<TLBytes> GetAutoSaveAsync(long userId)
    {
        IReadOnlyCollection<TLAutoSaveSettingsState> rows =
            await _repository.GetAutoSaveSettingsAsync(userId);
        var exceptions = new List<(DialogPeerKey Peer,
            TLSavePreset Settings)>();
        TLSavePreset? users = null;
        TLSavePreset? chats = null;
        TLSavePreset? broadcasts = null;
        try
        {
            foreach (TLAutoSaveSettingsState row in rows)
            {
                var value = row.AsAutoSaveSettingsState();
                TLSavePreset setting = value.Get_SettingsView()
                    .AsAutoSaveSettings().Clone().Build();
                switch (value.Scope)
                {
                    case AutoSaveUsers:
                        users?.Dispose();
                        users = setting;
                        break;
                    case AutoSaveChats:
                        chats?.Dispose();
                        chats = setting;
                        break;
                    case AutoSaveBroadcasts:
                        broadcasts?.Dispose();
                        broadcasts = setting;
                        break;
                    case AutoSavePeer:
                        exceptions.Add((new DialogPeerKey(
                            (TLPeer.PeerType)value.PeerType, value.PeerId),
                            setting));
                        break;
                    default:
                        setting.Dispose();
                        break;
                }
            }
        }
        finally
        {
            foreach (TLAutoSaveSettingsState row in rows) row.Dispose();
        }

        users ??= BuildEmptyAutoSave();
        chats ??= BuildEmptyAutoSave();
        broadcasts ??= BuildEmptyAutoSave();
        var hydratedUsers = new List<TLBytes>();
        var hydratedChats = new List<TLBytes>();
        foreach ((DialogPeerKey key, _) in exceptions)
        {
            if (key.Type == TLPeer.PeerType.PeerUser &&
                _userRepository.GetUser(key.Id) is { } user)
            {
                hydratedUsers.Add(user);
            }
            else if (key.Type is TLPeer.PeerType.PeerChat or
                     TLPeer.PeerType.PeerChannel &&
                     await _chatRepository.GetChatAsync(key.Id)
                         is { } chat)
            {
                hydratedChats.Add(chat);
            }
        }

        var usersVector = new Vector();
        var chatsVector = new Vector();
        var exceptionVector = new Vector();
        try
        {
            foreach ((DialogPeerKey key, TLSavePreset settings) in exceptions)
            {
                using TLPeer peer = PeerResolver.BuildPeer(key.Type, key.Id);
                using AutoSaveException exception = AutoSaveException.Builder()
                    .Peer(peer.AsSpan()).Settings(settings.AsSpan()).Build();
                exceptionVector.AppendTLObject(exception.ToReadOnlySpan());
            }
            foreach (TLBytes user in hydratedUsers)
                usersVector.AppendTLObject(user.AsSpan());
            foreach (TLBytes chat in hydratedChats)
                chatsVector.AppendTLObject(chat.AsSpan());

            return AccountAutoSaveSettings.Builder()
                .UsersSettings(users.Value.AsSpan())
                .ChatsSettings(chats.Value.AsSpan())
                .BroadcastsSettings(broadcasts.Value.AsSpan())
                .Exceptions(exceptionVector).Chats(chatsVector).Users(usersVector)
                .Build().TLBytes!.Value;
        }
        finally
        {
            users?.Dispose();
            chats?.Dispose();
            broadcasts?.Dispose();
            foreach (var exception in exceptions) exception.Settings.Dispose();
            foreach (TLBytes user in hydratedUsers) user.Dispose();
            foreach (TLBytes chat in hydratedChats) chat.Dispose();
        }
    }

    public async Task<TLBool> SaveAutoSaveAsync(long userId, int scope,
        DialogPeerKey peer, TLSavePreset settings)
    {
        using TLAutoSaveSettingsState row = AutoSaveSettingsState.Builder()
            .UserId(userId).Scope(scope).PeerType((int)peer.Type).PeerId(peer.Id)
            .Settings(settings.AsSpan()).Date(Now()).Build();
        return _repository.PutAutoSaveSettings(row) &&
               await _transactions.SaveAsync()
            ? BoolTrue.Builder().Build()
            : BoolError(500, "INTERNAL_SERVER_ERROR"u8);
    }

    public async Task<TLBool> DeleteAutoSaveExceptionsAsync(long userId)
    {
        IReadOnlyCollection<TLAutoSaveSettingsState> rows =
            await _repository.GetAutoSaveSettingsAsync(userId);
        bool success = true;
        foreach (TLAutoSaveSettingsState row in rows)
        {
            using (row)
            {
                var value = row.AsAutoSaveSettingsState();
                if (value.Scope == AutoSavePeer)
                {
                    success &= _repository.DeleteAutoSaveSettings(userId,
                        value.Scope, value.PeerType, value.PeerId);
                }
            }
        }
        return success && await _transactions.SaveAsync()
            ? BoolTrue.Builder().Build()
            : BoolError(500, "INTERNAL_SERVER_ERROR"u8);
    }

    public async Task<TLBytes> GetContentSettingsAsync(long userId)
    {
        using TLAccountSettingsState? stored = await _repository
            .GetSettingsAsync(userId);
        bool enabled = stored is not null &&
                       stored.Value.AsAccountSettingsState().SensitiveEnabled;
        return ContentSettings.Builder().SensitiveEnabled(enabled)
            .SensitiveCanChange(true).Build().TLBytes!.Value;
    }

    public async Task<TLBool> SetContentSettingsAsync(long userId, bool enabled) =>
        await MutateSettingsAsync(userId, sensitive: enabled) is null
            ? BoolTrue.Builder().Build()
            : BoolError(500, "INTERNAL_SERVER_ERROR"u8);

    public async Task<TLBytes> GetGlobalPrivacyAsync(long userId)
    {
        using TLAccountSettingsState? stored = await _repository
            .GetSettingsAsync(userId);
        if (stored is not null)
        {
            var row = stored.Value.AsAccountSettingsState();
            if (row.Flags[1])
            {
                return row.Get_GlobalPrivacySettingsView()
                    .AsGlobalPrivacySettings().Clone().Build().TLBytes!.Value;
            }
        }
        return GlobalPrivacySettings.Builder().Build().TLBytes!.Value;
    }

    public async Task<TLBytes> SetGlobalPrivacyAsync(long userId,
        TLGlobalPrivacySettings settings)
    {
        string? error = await MutateSettingsAsync(userId, privacy: settings);
        return error is null
            ? settings.AsGlobalPrivacySettings().Clone().Build().TLBytes!.Value
            : RpcErrorGenerator.GenerateError(500, "INTERNAL_SERVER_ERROR"u8);
    }

    public async Task<TLBool> SetAuthorizationTtlAsync(long userId, int days)
    {
        if (days is < 1 or > 366)
        {
            return BoolError(400, "TTL_DAYS_INVALID"u8);
        }
        return await MutateSettingsAsync(userId, authorizationTtlDays: days)
                   is null
            ? BoolTrue.Builder().Build()
            : BoolError(500, "INTERNAL_SERVER_ERROR"u8);
    }

    public async Task<int> GetAuthorizationTtlAsync(long userId)
    {
        using TLAccountSettingsState? stored = await _repository
            .GetSettingsAsync(userId);
        int days = stored?.AsAccountSettingsState().AuthorizationTtlDays ?? 0;
        return days is >= 1 and <= 366 ? days : DefaultAuthorizationTtlDays;
    }

    public async Task<TLBytes> GetReactionsNotifySettingsAsync(long userId)
    {
        using TLAccountSettingsState? stored = await _repository
            .GetSettingsAsync(userId);
        if (stored is not null)
        {
            var row = stored.Value.AsAccountSettingsState();
            if (row.Flags[2])
            {
                return row.Get_ReactionsNotifySettingsView()
                    .AsReactionsNotifySettings().Clone().Build().TLBytes!.Value;
            }
        }
        return BuildDefaultReactions().TLBytes!.Value;
    }

    public async Task<TLBytes> SetReactionsNotifySettingsAsync(long userId,
        TLReactionsNotifySettings settings)
    {
        string? error = await MutateSettingsAsync(userId, reactions: settings);
        return error is null
            ? settings.AsReactionsNotifySettings().Clone().Build().TLBytes!.Value
            : RpcErrorGenerator.GenerateError(500, "INTERNAL_SERVER_ERROR"u8);
    }

    public async Task<TLBytes> InitTakeoutAsync(long authKeyId, long userId,
        bool contacts, bool messageUsers, bool messageChats,
        bool messageMegagroups, bool messageChannels, bool files,
        long fileMaxSize)
    {
        using TLTakeoutSessionState? current = await _repository
            .GetTakeoutSessionByAuthKeyAsync(authKeyId);
        int now = Now();
        if (current is not null)
        {
            var existing = current.Value.AsTakeoutSessionState();
            if (existing.ExpiresAt > now)
            {
                return RpcErrorGenerator.GenerateError(400,
                    "TAKEOUT_INIT_DELAY_86400"u8);
            }
            _repository.DeleteTakeoutSession(existing.Id);
        }

        long id;
        do id = _random.NextLong() & long.MaxValue; while (id == 0);
        var builder = TakeoutSessionState.Builder().Contacts(contacts)
            .MessageUsers(messageUsers).MessageChats(messageChats)
            .MessageMegagroups(messageMegagroups)
            .MessageChannels(messageChannels).Files(files).Id(id).UserId(userId)
            .AuthKeyId(authKeyId).CreatedAt(now).ExpiresAt(now + 24 * 60 * 60);
        if (files) builder = builder.FileMaxSize(fileMaxSize);
        using TLTakeoutSessionState state = builder.Build();
        if (!_repository.PutTakeoutSession(state) ||
            !await _transactions.SaveAsync())
        {
            return RpcErrorGenerator.GenerateError(500,
                "INTERNAL_SERVER_ERROR"u8);
        }
        return Takeout.Builder().Id(id).Build().TLBytes!.Value;
    }

    public async Task<TLBool> FinishTakeoutAsync(long authKeyId, bool success)
    {
        using TLTakeoutSessionState? current = await _repository
            .GetTakeoutSessionByAuthKeyAsync(authKeyId);
        if (current is null || current.Value.AsTakeoutSessionState().ExpiresAt <=
            Now())
        {
            return BoolError(400, "TAKEOUT_INVALID"u8);
        }
        long id = current.Value.AsTakeoutSessionState().Id;
        return _repository.DeleteTakeoutSession(id) &&
               await _transactions.SaveAsync()
            ? BoolTrue.Builder().Build()
            : BoolError(500, "INTERNAL_SERVER_ERROR"u8);
    }

    private async Task<string?> MutateSettingsAsync(long userId,
        bool? sensitive = null, TLGlobalPrivacySettings? privacy = null,
        TLReactionsNotifySettings? reactions = null,
        int? authorizationTtlDays = null)
    {
        using TLAccountSettingsState? stored = await _repository
            .GetSettingsAsync(userId);
        bool currentSensitive = stored is not null &&
            stored.Value.AsAccountSettingsState().SensitiveEnabled;
        int currentTtl = stored?.AsAccountSettingsState().AuthorizationTtlDays ??
                         DefaultAuthorizationTtlDays;
        var builder = AccountSettingsState.Builder()
            .SensitiveEnabled(sensitive ?? currentSensitive).UserId(userId)
            .AuthorizationTtlDays(authorizationTtlDays ?? currentTtl).Date(Now());

        if (privacy is not null)
        {
            builder = builder.GlobalPrivacySettings(privacy.Value.AsSpan());
        }
        else if (stored is not null &&
                 stored.Value.AsAccountSettingsState().Flags[1])
        {
            builder = builder.GlobalPrivacySettings(
                stored.Value.AsAccountSettingsState().GlobalPrivacySettings);
        }
        if (reactions is not null)
        {
            builder = builder.ReactionsNotifySettings(reactions.Value.AsSpan());
        }
        else if (stored is not null &&
                 stored.Value.AsAccountSettingsState().Flags[2])
        {
            builder = builder.ReactionsNotifySettings(
                stored.Value.AsAccountSettingsState().ReactionsNotifySettings);
        }

        using TLAccountSettingsState next = builder.Build();
        return _repository.PutSettings(next) && await _transactions.SaveAsync()
            ? null : "INTERNAL_SERVER_ERROR";
    }

    private static TLDownloadPreset BuildDownloadPreset(int photo,
        long video, long file, bool disabled)
    {
        var builder = AutoDownloadSettings.Builder().PhotoSizeMax(photo)
            .VideoSizeMax(video).FileSizeMax(file).VideoUploadMaxbitrate(0)
            .SmallQueueActiveOperationsMax(0).LargeQueueActiveOperationsMax(0);
        if (disabled) builder = builder.Disabled(true);
        return builder.Build();
    }

    private static TLSavePreset BuildEmptyAutoSave() =>
        AutoSaveSettings.Builder().Build();

    private static ReactionsNotifySettings BuildDefaultReactions()
    {
        using NotificationSoundDefault sound = NotificationSoundDefault.Builder()
            .Build();
        return ReactionsNotifySettings.Builder().Sound(sound.ToReadOnlySpan())
            .ShowPreviews(true).Build();
    }

    private int Now() => (int)_time.GetUtcNow().ToUnixTimeSeconds();

    public static TLBool BoolError(int code, ReadOnlySpan<byte> message) =>
        (TLBool)RpcErrorGenerator.GenerateError(code, message);
}
