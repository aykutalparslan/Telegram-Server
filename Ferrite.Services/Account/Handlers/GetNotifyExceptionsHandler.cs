// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.account;
using Ferrite.TL.baseLayer.dto;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Vector = Ferrite.TL.Vector;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class GetNotifyExceptionsHandler : AccountSettingsHandlerBase
{
    private readonly INotifySettingsRepository _notifySettingsRepository;

    private readonly IChatRepository _chatRepository;
    private readonly IUserRepository _userRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _time;

    public GetNotifyExceptionsHandler(AccountSettingsStore store,
        IUnitOfWork unitOfWork, INotifySettingsRepository notifySettingsRepository, IChatRepository chatRepository, IUserRepository userRepository, TimeProvider time) : base(store)
    {
        _notifySettingsRepository = notifySettingsRepository;

        _chatRepository = chatRepository;
        _userRepository = userRepository;

        _unitOfWork = unitOfWork;
        _time = time;
    }

    [TLFunction(Constructors.baseLayer_GetNotifyExceptions)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        NotifyFilter filter = ReadFilter(q);
        long? userId = await GetUserIdAsync(authKeyId);
        if (userId is null) return AuthError();

        IReadOnlyCollection<TLNotifySettingsState> rows = _notifySettingsRepository.GetNotifyExceptions(authKeyId);
        var exceptions = new List<NotifyException>();
        try
        {
            foreach (TLNotifySettingsState state in rows)
            {
                NotifySettingsState row = state.AsNotifySettingsState();
                if (!filter.Matches(row.NotifyPeerType, row.PeerType,
                        row.PeerId)) continue;
                TLPeerNotifySettings settings = row.Get_Settings();
                if (!HasDifference(settings, filter.CompareSound,
                        filter.CompareStories))
                {
                    settings.Dispose();
                    continue;
                }
                exceptions.Add(new NotifyException(row.NotifyPeerType,
                    row.PeerType, row.PeerId, settings));
            }
        }
        finally
        {
            foreach (TLNotifySettingsState state in rows) state.Dispose();
        }

        var hydratedUsers = new List<TLUser>();
        var hydratedChats = new List<TLChat>();
        try
        {
            foreach (NotifyException item in exceptions)
            {
                if (item.NotifyPeerType != (int)InputNotifyPeerType.Peer)
                    continue;
                if (item.PeerType is (int)InputPeerType.User or
                    (int)InputPeerType.Self)
                {
                    long id = item.PeerType == (int)InputPeerType.Self
                        ? userId.Value : item.PeerId;
                    if (_userRepository.GetUser(id) is { } user)
                        hydratedUsers.Add(user);
                }
                else if (item.PeerType is (int)InputPeerType.Chat or
                         (int)InputPeerType.Channel)
                {
                    if (await _chatRepository.GetChatAsync(item.PeerId)
                        is { } chat) hydratedChats.Add(chat);
                }
            }

            var updates = new Vector();
            foreach (NotifyException item in exceptions)
            {
                using TLNotifyPeer peer = BuildNotifyPeer(item, userId.Value);
                using TLUpdate update = UpdateNotifySettings.Builder()
                    .Peer(peer.AsSpan()).NotifySettings(item.Settings.AsSpan())
                    .Build();
                updates.AppendTLObject(update.AsSpan());
            }
            var users = new Vector();
            foreach (TLUser user in hydratedUsers)
                users.AppendTLObject(user.AsSpan());
            var chats = new Vector();
            foreach (TLChat chat in hydratedChats)
                chats.AppendTLObject(chat.AsSpan());

            return Updates.Builder().UpdatesProperty(updates).Users(users)
                .Chats(chats)
                .Date((int)_time.GetUtcNow().ToUnixTimeSeconds()).Seq(0)
                .Build().TLBytes!.Value;
        }
        finally
        {
            foreach (NotifyException item in exceptions) item.Dispose();
            foreach (TLUser user in hydratedUsers) user.Dispose();
            foreach (TLChat chat in hydratedChats) chat.Dispose();
        }
    }

    private static NotifyFilter ReadFilter(TLBytes q)
    {
        var request = new GetNotifyExceptions(q.AsSpan());
        if (!request.Flags[0])
            return new NotifyFilter(request.CompareSound,
                request.CompareStories, null, null, null);

        InputNotifyPeerView peer = request.Get_PeerView();
        if (peer.Is(out InputNotifyUsers _))
            return new NotifyFilter(request.CompareSound,
                request.CompareStories, (int)InputNotifyPeerType.Users, null,
                null);
        if (peer.Is(out InputNotifyChats _))
            return new NotifyFilter(request.CompareSound,
                request.CompareStories, (int)InputNotifyPeerType.Chats, null,
                null);
        if (peer.Is(out InputNotifyBroadcasts _))
            return new NotifyFilter(request.CompareSound,
                request.CompareStories, (int)InputNotifyPeerType.Broadcasts,
                null, null);
        if (!peer.Is(out InputNotifyPeer exact))
            return new NotifyFilter(request.CompareSound,
                request.CompareStories, int.MinValue, null, null);

        InputPeerView target = exact.Get_PeerView();
        return target.Type switch
        {
            TLInputPeer.InputPeerType.InputPeerSelf => new NotifyFilter(
                request.CompareSound, request.CompareStories,
                (int)InputNotifyPeerType.Peer, (int)InputPeerType.Self, 0),
            TLInputPeer.InputPeerType.InputPeerUser => new NotifyFilter(
                request.CompareSound, request.CompareStories,
                (int)InputNotifyPeerType.Peer, (int)InputPeerType.User,
                target.AsInputPeerUser().UserId),
            TLInputPeer.InputPeerType.InputPeerChat => new NotifyFilter(
                request.CompareSound, request.CompareStories,
                (int)InputNotifyPeerType.Peer, (int)InputPeerType.Chat,
                target.AsInputPeerChat().ChatId),
            TLInputPeer.InputPeerType.InputPeerChannel => new NotifyFilter(
                request.CompareSound, request.CompareStories,
                (int)InputNotifyPeerType.Peer, (int)InputPeerType.Channel,
                target.AsInputPeerChannel().ChannelId),
            _ => new NotifyFilter(request.CompareSound, request.CompareStories,
                int.MinValue, null, null),
        };
    }

    private static bool HasDifference(TLPeerNotifySettings settings,
        bool compareSound, bool compareStories)
    {
        PeerNotifySettings value = settings.AsPeerNotifySettings();
        bool messageDifference = value.Flags[0] || value.Flags[1] ||
                                 value.Flags[2];
        bool soundDifference = compareSound && (value.Flags[3] ||
            value.Flags[4] || value.Flags[5]);
        bool storyDifference = compareStories && (value.Flags[6] ||
            value.Flags[7] || value.Flags[8] || value.Flags[9] ||
            value.Flags[10]);
        return messageDifference || soundDifference || storyDifference;
    }

    private static TLNotifyPeer BuildNotifyPeer(NotifyException item,
        long userId)
    {
        if (item.NotifyPeerType == (int)InputNotifyPeerType.Users)
            return NotifyUsers.Builder().Build();
        if (item.NotifyPeerType == (int)InputNotifyPeerType.Chats)
            return NotifyChats.Builder().Build();
        if (item.NotifyPeerType == (int)InputNotifyPeerType.Broadcasts)
            return NotifyBroadcasts.Builder().Build();

        using TLPeer peer = item.PeerType switch
        {
            (int)InputPeerType.Chat => PeerChat.Builder().ChatId(item.PeerId)
                .Build(),
            (int)InputPeerType.Channel => PeerChannel.Builder()
                .ChannelId(item.PeerId).Build(),
            (int)InputPeerType.Self => PeerUser.Builder().UserId(userId).Build(),
            _ => PeerUser.Builder().UserId(item.PeerId).Build(),
        };
        return NotifyPeer.Builder().Peer(peer.AsSpan()).Build();
    }

    private readonly record struct NotifyFilter(bool CompareSound,
        bool CompareStories, int? NotifyPeerType, int? PeerType, long? PeerId)
    {
        public bool Matches(int notifyPeerType, int peerType, long peerId) =>
            (NotifyPeerType is null || NotifyPeerType == notifyPeerType) &&
            (PeerType is null || PeerType == peerType) &&
            (PeerId is null || PeerId == peerId);
    }

    private readonly record struct NotifyException(int NotifyPeerType,
        int PeerType, long PeerId, TLPeerNotifySettings Settings) : IDisposable
    {
        public void Dispose() => Settings.Dispose();
    }
}
