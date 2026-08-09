// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.users;
using TLUserFull = Ferrite.TL.baseLayer.users.TLUserFull;

namespace Ferrite.Services.Handlers.UserMethods;

public sealed class GetFullUserHandler : UserHandlerBase
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly PrivacyEvaluator _privacyEvaluator;
    private readonly ChatSettingsStore _settings;
    private readonly ModerationStore _moderation;
    private readonly AccountAudioStore _audio;

    public GetFullUserHandler(IUnitOfWork unitOfWork, IAppInfoRepository appInfoRepository, IAuthorizationRepository authorizationRepository, INotifySettingsRepository notifySettingsRepository, IPhotoRepository photoRepository, IUserRepository userRepository, IUserStatusRepository userStatusRepository,
        PrivacyEvaluator privacyEvaluator, ChatSettingsStore settings,
        ModerationStore moderation, AccountAudioStore audio,
        ProfileStore? profiles = null)
        : base(unitOfWork, appInfoRepository, notifySettingsRepository, photoRepository, userRepository, userStatusRepository, profiles)
    {
        _authorizationRepository = authorizationRepository;

        _privacyEvaluator = privacyEvaluator;
        _settings = settings;
        _moderation = moderation;
        _audio = audio;
    }

    [TLFunction(Constructors.baseLayer_GetFullUser)]
    public async ValueTask<TLUserFull> Handle(long authKeyId, TLBytes q)
        {
            var (userId, constructor) = GetUserId(((GetFullUser)q).Id);
            var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
            if (auth == null) return (TLUserFull)RpcErrorGenerator.GenerateError(400, "AUTH_KEY_INVALID"u8);
            long viewerUserId = auth.Value.AsAuthInfo().UserId;
            if (constructor == Constructors.baseLayer_InputUserSelf)
            {
                userId = viewerUserId;
            }
            using var user = await GetUserInternal(userId, viewerUserId);
            DeviceType deviceType = GetDeviceType(authKeyId);
            using var notifySettings = GetPeerNotifySettings(authKeyId, user, deviceType);

            if (user != null)
            {
                var target = user.Value.AsUser();
                bool callableUserShape = target.Constructor == Constructors.baseLayer_User &&
                                    !target.Bot && !target.Deleted &&
                                    target.Id != viewerUserId;

                bool phoneCallsAvailable = false;
                bool videoCallsAvailable = false;
                bool phoneCallsPrivate = false;
                if (callableUserShape)
                {
                    CallPrivacyDecision decision = await _privacyEvaluator
                        .EvaluatePhoneCall(viewerUserId, userId);
                    phoneCallsAvailable = true;
                    videoCallsAvailable = true;
                    phoneCallsPrivate = decision != CallPrivacyDecision.Allowed;
                }

                ChatSettingsSnapshot conversation = await _settings.GetAsync(
                    ChatSettingsScope.ForPrivatePair(viewerUserId, userId));
                using TLPeerWallpaper? wallpaper = await _settings
                    .GetPrivateWallpaperAsync(viewerUserId, userId);
                using TLAccountProfileState? profile = _profiles is null
                    ? null : await _profiles.GetProfileAsync(userId);
                using TLDocument? savedMusic = await _audio
                    .GetFirstMusicAsync(userId);
                // These settings become the client's action bar for this private
                // chat, so they answer the same question `messages.getPeerSettings`
                // does, dismissal included.
                bool offerActionBar = await _moderation
                    .ShouldOfferPrivateActionBarAsync(viewerUserId, userId);
                return CreteFullUser(user.Value, notifySettings, phoneCallsAvailable,
                    videoCallsAvailable, phoneCallsPrivate, offerActionBar,
                    conversation.ThemeEmoticon, conversation.TtlPeriod, wallpaper,
                    profile, savedMusic);
            }

            return (TLUserFull)RpcErrorGenerator.GenerateError(400, "USER_ID_INVALID"u8);
        }
}
