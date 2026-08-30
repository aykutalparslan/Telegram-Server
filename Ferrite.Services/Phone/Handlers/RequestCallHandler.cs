// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Crypto;
using Ferrite.Data.Repositories;
using Ferrite.Services.Calls;
using Ferrite.Services.Calls;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.phone;
using Ferrite.Utils;
using PhoneResult = Ferrite.TL.baseLayer.phone.TLPhoneCall;

namespace Ferrite.Services.Phone.Handlers;

public sealed class RequestCallHandler : PhoneCallHandlerBase
{
    private readonly IUserRepository _userRepository;
    private readonly UserSerializer _userSerializer;

    private const int GaHashLength = 32;

    private readonly PrivacyEvaluator _privacy;
    private readonly CallRegistryOptions _options;

    public RequestCallHandler(IUnitOfWork unitOfWork, IBlockedPeersRepository blockedPeersRepository, IAuthorizationRepository authorizationRepository, IUserRepository userRepository, UserSerializer userSerializer, ICallRegistry registry,
        IUpdatesService updates, IMTProtoTime time, PrivacyEvaluator privacy,
        CallRegistryOptions options)
        : base(unitOfWork, blockedPeersRepository, authorizationRepository, userRepository, registry, updates, time)
    {
        _userRepository = userRepository;
        _userSerializer = userSerializer;

        _privacy = privacy;
        _options = options;
    }

    [TLFunction(Constructors.baseLayer_RequestCall)]
    public async ValueTask<PhoneResult> Handle(long authKeyId, TLBytes q)
    {
        long targetUserId;
        long? targetAccessHash;
        bool targetsSelf;
        var request = (RequestCall)q;
        bool video = request.Video;
        int randomId = request.RandomId;
        byte[] gaHash = request.GAHash.ToArray();
        CallProtocol protocol = ReadProtocol(request.Protocol);
        InputUserView target = request.Get_UserIdView();
        if (target.Is(out InputUserSelf _))
        {
            targetsSelf = true;
            targetUserId = 0;
            targetAccessHash = null;
        }
        else if (target.Is(out InputUser inputUser))
        {
            targetsSelf = false;
            targetUserId = inputUser.UserId;
            targetAccessHash = inputUser.AccessHash;
        }
        else
        {
            targetsSelf = false;
            targetUserId = 0;
            targetAccessHash = null;
        }

        long? callerUserIdValue = await GetCurrentUserIdAsync(authKeyId);
        if (callerUserIdValue is not long callerUserId)
        {
            return Error(400, "AUTH_KEY_INVALID"u8);
        }

        if (targetsSelf || targetUserId <= 0 || targetUserId == callerUserId)
        {
            return Error(400, "USER_ID_INVALID"u8);
        }

        if (gaHash.Length != GaHashLength)
        {
            return Error(400, "GA_HASH_INVALID"u8);
        }

        if (!ValidateProtocol(protocol, out PhoneResult protocolError))
        {
            return protocolError;
        }

        if (!TryValidateTarget(targetUserId, targetAccessHash))
        {
            return Error(400, "USER_ID_INVALID"u8);
        }

        CallPrivacyDecision decision = await _privacy.EvaluatePhoneCall(callerUserId,
            targetUserId);
        switch (decision)
        {
            case CallPrivacyDecision.Blocked:
                return Error(403, "USER_IS_BLOCKED"u8);
            case CallPrivacyDecision.PrivacyRestricted:
                return Error(403, "USER_PRIVACY_RESTRICTED"u8);
        }

        int date = Now();
        var createRequest = new CallCreateRequest(callerUserId, authKeyId,
            targetUserId, randomId, gaHash, protocol, video, date);
        CallRegistryResult result = Registry.TryCreate(createRequest);
        switch (result.Status)
        {
            case CallRegistryStatus.Ok:
                break;
            case CallRegistryStatus.Duplicate:
                return BuildResult(callerUserId, BuildWaiting(result.Call!), callerUserId,
                    targetUserId, _userSerializer);
            case CallRegistryStatus.DedupConflict:
                return Error(400, "RANDOM_ID_DUPLICATE"u8);
            case CallRegistryStatus.QuotaExceeded:
            case CallRegistryStatus.RegistryFull:
            case CallRegistryStatus.RateLimited:
                return Error(400, "CALL_OCCUPY_FAILED"u8);
            default:
                return Error(400, "CALL_OCCUPY_FAILED"u8);
        }

        CallSnapshot call = result.Call!;
        await PushCallUpdate(targetUserId, BuildRequested(call),
            UpdateDeliveryScope.All);
        return BuildResult(callerUserId, BuildWaiting(call), callerUserId, targetUserId,
            _userSerializer);
    }

    private bool ValidateProtocol(CallProtocol protocol, out PhoneResult error)
    {
        CallProtocolError protocolError =
            CallProtocolNegotiator.ValidateOffer(protocol, _options);
        error = protocolError switch
        {
            CallProtocolError.None => default,
            CallProtocolError.FlagsInvalid =>
                Error(400, "CALL_PROTOCOL_FLAGS_INVALID"u8),
            CallProtocolError.LayerInvalid =>
                Error(400, "CALL_PROTOCOL_LAYER_INVALID"u8),
            CallProtocolError.VersionOutdated =>
                Error(400, "PARTICIPANT_VERSION_OUTDATED"u8),
            _ => Error(400, "CALL_PROTOCOL_COMPAT_LAYER_INVALID"u8),
        };
        return protocolError == CallProtocolError.None;
    }

    private bool TryValidateTarget(long targetUserId, long? targetAccessHash)
    {
        TLUser? targetValue = _userRepository.GetUser(targetUserId);
        if (targetValue is null)
        {
            return false;
        }

        using TLUser targetOwned = targetValue.Value;
        if (targetOwned.Constructor != Constructors.baseLayer_User)
        {
            return false;
        }

        User target = targetOwned.AsUser();
        if (target.Deleted || target.Bot || target.Id != targetUserId)
        {
            return false;
        }

        if (targetAccessHash is long accessHash &&
            (!target.Flags[0] || target.AccessHash != accessHash))
        {
            return false;
        }

        return true;
    }
}
