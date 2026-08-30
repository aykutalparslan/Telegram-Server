// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.users;
using TLUserFull = Ferrite.TL.baseLayer.users.TLUserFull;

namespace Ferrite.Services.Handlers.UserMethods;

public abstract class UserHandlerBase
{
    private readonly IAppInfoRepository _appInfoRepository;
    private readonly INotifySettingsRepository _notifySettingsRepository;
    private readonly IPhotoRepository _photoRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUserStatusRepository _userStatusRepository;
    private readonly UserSerializer _userSerializer;

    protected readonly IUnitOfWork _unitOfWork;
    protected readonly ProfileStore? _profiles;

    protected UserHandlerBase(IUnitOfWork unitOfWork, IAppInfoRepository appInfoRepository, IContactsRepository contactsRepository, INotifySettingsRepository notifySettingsRepository, IPhotoRepository photoRepository, IUserRepository userRepository, IUserStatusRepository userStatusRepository,
        ProfileStore? profiles = null)
    {
        _appInfoRepository = appInfoRepository;
        _notifySettingsRepository = notifySettingsRepository;
        _photoRepository = photoRepository;
        _userRepository = userRepository;
        _userStatusRepository = userStatusRepository;
        _userSerializer = new UserSerializer(userRepository, userStatusRepository, contactsRepository);

        _unitOfWork = unitOfWork;
        _profiles = profiles;
    }

    protected async ValueTask<List<byte[]>> GetUsersFromRepo(
        List<InputUserRequest> requests, long selfUserId)
    {
        List<byte[]> result = new();
        foreach (var request in requests)
        {
            long userId = request.Self ? selfUserId : request.UserId;
            if (userId == 0)
            {
                continue;
            }

            using var user = await GetUserInternal(selfUserId, userId);
            if (user != null)
            {
                result.Add(user.Value.AsSpan().ToArray());
            }
        }

        return result;
    }

    protected readonly record struct InputUserRequest(long UserId, bool Self);

    protected List<InputUserRequest> GetUserIds(TLBytes q)
    {
        List<InputUserRequest> ids = new();
        var users = ((GetUsers)q).Id;
        for (int i = 0; i < users.Count; i++)
        {
            var user = users.ReadTLObject();
            var (userId, constructor) = GetUserId(user);
            ids.Add(new InputUserRequest(userId,
                constructor == Constructors.baseLayer_InputUserSelf));
        }

        return ids;
    }

    protected static (long, int) GetUserId(Span<byte> user)
    {
        var view = (InputUserView)user;
        if (view.Is(out InputUser inputUser)) return (inputUser.UserId, view.Constructor);
        if (view.Is(out InputUserFromMessage fromMessage))
        {
            return (fromMessage.UserId, view.Constructor);
        }
        return (0, view.Constructor);
    }

    protected TLUserFull CreteFullUser(TLUser userBytes,
        TLPeerNotifySettings notifySettings, bool phoneCallsAvailable,
        bool videoCallsAvailable, bool phoneCallsPrivate,
        bool offerActionBar, string? themeEmoticon = null, int ttlPeriod = 0,
        TLPeerWallpaper? wallpaper = null,
        TLAccountProfileState? profileState = null,
        TLDocument? savedMusic = null)
    {
        var user = userBytes.AsUser();
        var photo = user.Get_PhotoView();
        int photoConstructor = photo.Constructor;
        bool hasProfilePhoto = photoConstructor != 0 &&
                               photoConstructor != Constructors.baseLayer_UserProfilePhotoEmpty;
        long photoId = !hasProfilePhoto ? 0 : photo.AsUserProfilePhoto().PhotoId;
        using var settings = GeneratePeerSettings(offerActionBar);
        string? about = _userRepository.GetAbout(user.Id);
        var userfull = UserFull.Builder()
            .Id(user.Id)
            .Blocked(false)
            .Settings(settings.AsSpan())
            .NotifySettings(notifySettings.AsSpan())
            .PhoneCallsAvailable(phoneCallsAvailable)
            .VideoCallsAvailable(videoCallsAvailable)
            .PhoneCallsPrivate(phoneCallsPrivate)
            .CommonChatsCount(0);
        if (!hasProfilePhoto)
        {
            using var profilePhoto = PhotoEmpty.Builder().Build();
            userfull = userfull.ProfilePhoto(profilePhoto.ToReadOnlySpan());
        }
        else if (_photoRepository.GetProfilePhoto(user.Id, photoId)
                 is { } profilePhotoBytes)
        {
            using var profilePhoto = profilePhotoBytes;
            userfull = userfull.ProfilePhoto(profilePhoto.AsSpan());
        }
        if (about != null)
        {
            userfull = userfull.About(Encoding.UTF8.GetBytes(about));
        }
        using var theme = string.IsNullOrEmpty(themeEmoticon)
            ? default
            : ChatTheme.Builder()
                .Emoticon(Encoding.UTF8.GetBytes(themeEmoticon))
                .Build();
        if (!string.IsNullOrEmpty(themeEmoticon))
        {
            userfull = userfull.Theme(theme.ToReadOnlySpan());
        }
        if (ttlPeriod > 0)
        {
            userfull = userfull.TtlPeriod(ttlPeriod);
        }
        if (wallpaper is { } storedWallpaper)
        {
            var row = storedWallpaper.AsPeerWallpaper();
            if (row.Flags[2])
            {
                userfull = userfull.Wallpaper(row.Wallpaper);
            }
            if (row.Overridden)
            {
                userfull = userfull.WallpaperOverridden(true);
            }
        }
        if (profileState is { } storedProfile)
        {
            AccountProfileState profile = storedProfile.AsAccountProfileState();
            if (profile.Flags[4]) userfull = userfull.Birthday(profile.Birthday);
            if (profile.Flags[5])
            {
                userfull = userfull.PersonalChannelId(profile.PersonalChannelId)
                    .PersonalChannelMessage(0);
            }
            if (profile.Flags[6]) userfull = userfull.MainTab(profile.MainTab);
        }
        if (savedMusic is { } music) userfull = userfull.SavedMusic(music.AsSpan());

        using var finalUser = userfull.Build();
        var users = new Vector();
        users.AppendTLObject(userBytes.AsSpan());
        return UsersUserFull.Builder()
            .FullUser(finalUser.ToReadOnlySpan())
            .Users(users)
            .Chats(new Vector())
            .Build();
    }

    protected static TLPeerSettings GeneratePeerSettings(bool offerActionBar) =>
        offerActionBar
            ? PeerSettings.Builder()
                .ReportSpam(true).AddContact(true).BlockContact(true)
                .ShareContact(false).NeedContactsException(false).ReportGeo(false)
                .Autoarchived(false).InviteMembers(false).RequestChatBroadcast(false)
                .Build()
            : PeerSettings.Builder()
                .ReportSpam(false).AddContact(false).BlockContact(false)
                .ShareContact(false).NeedContactsException(false).ReportGeo(false)
                .Autoarchived(false).InviteMembers(false).RequestChatBroadcast(false)
                .Build();

    protected TLPeerNotifySettings GetPeerNotifySettings(long authKeyId,
        TLBytes? user, DeviceType deviceType)
    {
        if (user == null) return PeerNotifySettings.Builder().Build();
        var settings = _notifySettingsRepository.GetNotifySettings(
            authKeyId, (int)InputNotifyPeerType.Peer, (int)InputPeerType.User,
            ((User)user).Id, (int)deviceType);
        return settings.Count == 0 ? PeerNotifySettings.Builder().Build() : settings.First();
    }

    protected DeviceType GetDeviceType(long authKeyId)
    {
        DeviceType deviceType = DeviceType.Other;
        using var infoBytes = _appInfoRepository.GetAppInfo(authKeyId);
        if (infoBytes == null) return deviceType;
        var info = infoBytes.Value.AsAppInfo();
        string langPack = Encoding.UTF8.GetString(info.LangPack).ToLower();
        if (langPack.Contains("android")) deviceType = DeviceType.Android;
        else if (langPack.Contains("ios")) deviceType = DeviceType.iOS;
        return deviceType;
    }

    protected async ValueTask<TLUser?> GetUserInternal(long viewerUserId, long userId)
    {
        if (await _userSerializer.GetAsync(viewerUserId, userId) is not { } withStatus) return null;
        if (_profiles is null) return withStatus;
        using (withStatus)
        {
            return await _profiles.HydrateUserAsync(viewerUserId, userId,
                withStatus);
        }
    }
}
