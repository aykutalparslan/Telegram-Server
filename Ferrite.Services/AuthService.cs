// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Text;
using Ferrite.Crypto;
using Ferrite.Data;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.auth;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.mtproto;
using Ferrite.Utils;
using TLAuthorization = Ferrite.TL.baseLayer.auth.TLAuthorization;

namespace Ferrite.Services;

public class AuthService : IAuthService
{
    private readonly IAccountPasswordRepository _accountPasswordRepository;

    private readonly IAppInfoRepository _appInfoRepository;
    private readonly IAuthKeyRepository _authKeyRepository;
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IBoundAuthKeyRepository _boundAuthKeyRepository;
    private readonly ILoginAttemptRepository _loginAttemptRepository;
    private readonly IUserRepository _userRepository;

    protected readonly IRandomGenerator _random;
    protected readonly ISearchEngine _search;
    protected readonly IAtomicCounter _userIdCnt;
    protected readonly IUnitOfWork _unitOfWork;
    protected readonly IVerificationCodeService _verificationCodes;
    protected readonly IAuthorizationCompletion _authorizationCompletion;
    protected readonly ILoginTokenService _loginTokens;
    protected readonly TimeProvider _timeProvider;
    protected readonly ILogger _log;
    private static readonly TimeSpan LoginAttemptTtl = TimeSpan.FromMinutes(5);

    public AuthService(IRandomGenerator random, ISearchEngine search,
        IUnitOfWork unitOfWork, IAccountPasswordRepository accountPasswordRepository, IAppInfoRepository appInfoRepository, IAuthKeyRepository authKeyRepository, IAuthorizationRepository authorizationRepository, IBoundAuthKeyRepository boundAuthKeyRepository, ILoginAttemptRepository loginAttemptRepository, IUserRepository userRepository, ICounterFactory counterFactory,
        IVerificationCodeService verificationCodes,
        IAuthorizationCompletion authorizationCompletion,
        ILoginTokenService loginTokens,
        TimeProvider timeProvider,
        ILogger log)
    {
        _accountPasswordRepository = accountPasswordRepository;

        _appInfoRepository = appInfoRepository;
        _authKeyRepository = authKeyRepository;
        _authorizationRepository = authorizationRepository;
        _boundAuthKeyRepository = boundAuthKeyRepository;
        _loginAttemptRepository = loginAttemptRepository;
        _userRepository = userRepository;

        _random = random;
        _search = search;
        _userIdCnt = counterFactory.GetCounter("counter_user_id");
        _unitOfWork = unitOfWork;
        _verificationCodes = verificationCodes;
        _authorizationCompletion = authorizationCompletion;
        _loginTokens = loginTokens;
        _timeProvider = timeProvider;
        _log = log;
    }

    public async ValueTask<TLBool> BindTempAuthKey(long sessionId, TLBytes q)
    {
        var bindParameters = GetBindTempAuthKeyParameters(sessionId, q);
        if (bindParameters == null)
        {
            return (TLBool)RpcErrorGenerator.GenerateError(400, 
                "ENCRYPTED_MESSAGE_INVALID"u8);
        }

        if (await _boundAuthKeyRepository.GetBoundAuthKeyAsync(bindParameters.Value.TempAuthKeyId) != null)
        {
            return (TLBool)RpcErrorGenerator.GenerateError(400, 
                "TEMP_AUTH_KEY_ALREADY_BOUND"u8);
        }
        
        _boundAuthKeyRepository.PutBoundAuthKey(bindParameters.Value.TempAuthKeyId,
            bindParameters.Value.PermAuthKeyId, 
            new TimeSpan(0, 0, bindParameters.Value.ExpiresAt));
        var result = await _unitOfWork.SaveAsync();
        return result ? new BoolTrue() : new BoolFalse();
    }

    protected readonly record struct BindTempAuthKeyParameters(long TempAuthKeyId, long PermAuthKeyId, int ExpiresAt);

    protected BindTempAuthKeyParameters? GetBindTempAuthKeyParameters(long sessionId, TLBytes q)
    {
        using var bindRequest = new BindTempAuthKey(q.AsSpan());
        var authKey = _authKeyRepository.GetAuthKey(bindRequest.PermAuthKeyId);
        if (authKey == null) return null;
        Span<byte> encrypted = stackalloc byte[bindRequest.EncryptedMessage.Length];
        bindRequest.EncryptedMessage.CopyTo(encrypted);
        using var bindDataInner = DecryptBindingMessage(authKey, encrypted);
        if (bindDataInner.PermAuthKeyId != bindRequest.PermAuthKeyId ||
            bindDataInner.Nonce != bindRequest.Nonce ||
            bindDataInner.TempSessionId != sessionId) return null;
        return new BindTempAuthKeyParameters(bindDataInner.TempAuthKeyId, bindDataInner.PermAuthKeyId,
            bindRequest.ExpiresAt);
    }
    
    protected BindAuthKeyInner DecryptBindingMessage(Span<byte> authKey, Span<byte> encrypted)
    {
        Span<byte> messageKey = encrypted.Slice(8, 16);
        AesIgeV1 aesIge = new AesIgeV1(authKey, messageKey);
        aesIge.Decrypt(encrypted[24..]);
        return new BindAuthKeyInner(encrypted[(24 + 32)..]);
    }

    public async ValueTask<TLLoginToken> ExportLoginToken(long authKeyId, long sessionId, TLBytes q)
    {
        var tokenParameters = GetExportLoginTokenParameters(q);
        return await _loginTokens.ExportAsync(authKeyId, sessionId,
            tokenParameters.ApiId, tokenParameters.ApiHash,
            tokenParameters.ExceptIds.ToArray());
    }

    protected readonly record struct ExportLoginTokenParameters(int ApiId, string ApiHash, ICollection<long> ExceptIds);

    protected static ExportLoginTokenParameters GetExportLoginTokenParameters(TLBytes q)
    {
        var exportRequest = new ExportLoginToken(q.AsSpan());
        var apiHash = Encoding.UTF8.GetString(exportRequest.ApiHash);
        var ids = new long[exportRequest.ExceptIds.Count];
        for (int i = 0; i < exportRequest.ExceptIds.Count; i++)
        {
            ids[i] = exportRequest.ExceptIds[i];
        }
        return new ExportLoginTokenParameters(exportRequest.ApiId, apiHash, ids);
    }
    
    public async ValueTask<bool> IsAuthorized(long authKeyId)
    {
        if (authKeyId == 0)
        {
            return false;
        }

        var authKeyDetails = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
        if (authKeyDetails != null) return authKeyDetails.Value.AsAuthInfo().LoggedIn;
        var permAuthKey = await _boundAuthKeyRepository.GetBoundAuthKeyAsync(authKeyId);
        if (permAuthKey != null)
        {
            authKeyDetails = await _authorizationRepository.GetAuthorizationAsync((long)permAuthKey);
        }

        return authKeyDetails != null && authKeyDetails.Value.AsAuthInfo().LoggedIn;
    }

    public async ValueTask<TLAuthorization> SignIn(long authKeyId, TLBytes q)
    {
        var signInParameters = GetSignInParameters(q);
        var (phoneNumber, phoneCodeHash, phoneCode) = signInParameters;
        _log.Debug($"*** Sign In for authKey with Id: {authKeyId} ***");
        if (phoneCode == null)
        {
            return (TLAuthorization)RpcErrorGenerator.GenerateError(400,
                "EMAIL_VERIFY_INVALID"u8);
        }

        VerifiedChallenge? verified = await _verificationCodes.VerifyAsync(
            VerificationPurpose.LoginPhone, authKeyId, 0, phoneCodeHash,
            phoneCode);
        if (verified is not { } challenge ||
            !StringComparer.Ordinal.Equals(challenge.Destination, phoneNumber))
        {
            return (TLAuthorization)RpcErrorGenerator.GenerateError(400, "PHONE_CODE_INVALID"u8);
        }

        int apiLayer = -1;
        TLAuthInfo? existing = await _authorizationCompletion.ResolveAsync(
            authKeyId);
        using (existing)
        {
            if (existing is { } authorization)
            {
                apiLayer = authorization.AsAuthInfo().ApiLayer;
            }
        }

        int now = UnixNow();
        int expiresAt = checked(now + (int)LoginAttemptTtl.TotalSeconds);
        var userId = _userRepository.GetUserId(phoneNumber);
        using TLLoginAttempt attempt = BuildLoginAttempt(authKeyId,
            phoneNumber, phoneCodeHash, apiLayer, now, expiresAt, userId);
        await _loginAttemptRepository.PutAttemptAsync(attempt,
            LoginAttemptTtl);
        if(userId == null)
        {
            return AuthorizationSignUpRequired.Builder().Build();
        }

        if (!await _authorizationCompletion.CreateOrUpdatePendingAsync(
                authKeyId, userId.Value, phoneNumber, apiLayer))
        {
            return (TLAuthorization)RpcErrorGenerator.GenerateError(500,
                "INTERNAL"u8);
        }

        TLAccountPasswordState? password = await _accountPasswordRepository.GetPasswordStateAsync(userId.Value);
        using (password)
        {
            if (password is { } state &&
                state.AsAccountPasswordState().HasPassword)
            {
                return (TLAuthorization)RpcErrorGenerator.GenerateError(401,
                    "SESSION_PASSWORD_NEEDED"u8);
            }
        }

        TLAuthorization? completed = await _authorizationCompletion
            .CompleteAsync(authKeyId);
        return completed ?? (TLAuthorization)RpcErrorGenerator.GenerateError(
            500, "INTERNAL"u8);
    }

    protected static SignInParameters GetSignInParameters(TLBytes q)
    {
        var signIn = new SignIn(q.AsSpan());
        var phoneNumber = Encoding.UTF8.GetString(signIn.PhoneNumber);
        var phoneCodeHash = Encoding.UTF8.GetString(signIn.PhoneCodeHash);
        string? phoneCode = signIn.Flags[0]
            ? Encoding.UTF8.GetString(signIn.PhoneCode)
            : null;
        return new SignInParameters(phoneNumber, phoneCodeHash, phoneCode);
    }

    protected readonly record struct SignInParameters(string PhoneNumber,
        string PhoneCodeHash, string? PhoneCode);

    public async ValueTask<TLAuthorization> SignUp(long authKeyId, TLBytes q)
    {
        _log.Debug($"*** Sign Up for authKey with Id: {authKeyId} ***");
        var signUpParameters = GetSignUpParameters(q);
        TLLoginAttempt? found = await _loginAttemptRepository
            .GetByAuthKeyAsync(authKeyId);
        if (found is not { } pending)
        {
            return (TLAuthorization)RpcErrorGenerator.GenerateError(400,
                "PHONE_CODE_INVALID"u8);
        }

        bool matches;
        using (pending)
        {
            var view = pending.AsLoginAttempt();
            matches = view.AuthKeyId == authKeyId &&
                      view.ExpiresAt > UnixNow() &&
                      view.Phone.SequenceEqual(Encoding.UTF8.GetBytes(
                          signUpParameters.PhoneNumber)) &&
                      view.PhoneCodeHash.SequenceEqual(Encoding.UTF8.GetBytes(
                          signUpParameters.PhoneCodeHash));
        }
        if (!matches)
        {
            return (TLAuthorization)RpcErrorGenerator.GenerateError(400,
                "PHONE_CODE_INVALID"u8);
        }

        TLLoginAttempt? consumed = await _loginAttemptRepository
            .ConsumeByAuthKeyAsync(authKeyId);
        if (consumed is not { } attempt)
        {
            return (TLAuthorization)RpcErrorGenerator.GenerateError(400,
                "PHONE_CODE_INVALID"u8);
        }

        int apiLayer;
        using (attempt)
        {
            var view = attempt.AsLoginAttempt();
            if (view.ExpiresAt <= UnixNow())
            {
                return (TLAuthorization)RpcErrorGenerator.GenerateError(400,
                    "PHONE_CODE_EXPIRED"u8);
            }
            if (view.AuthKeyId != authKeyId ||
                !view.Phone.SequenceEqual(Encoding.UTF8.GetBytes(
                    signUpParameters.PhoneNumber)) ||
                !view.PhoneCodeHash.SequenceEqual(Encoding.UTF8.GetBytes(
                    signUpParameters.PhoneCodeHash)))
            {
                return (TLAuthorization)RpcErrorGenerator.GenerateError(400,
                    "PHONE_CODE_INVALID"u8);
            }
            if (view.Flags[0])
            {
                return (TLAuthorization)RpcErrorGenerator.GenerateError(400,
                    "PHONE_NUMBER_OCCUPIED"u8);
            }
            apiLayer = view.ApiLayer;
        }

        long userId = await _userIdCnt.IncrementAndGet();
        if(userId == 0)
        {
            userId = await _userIdCnt.IncrementAndGet();
        }
        
        using var user = SaveUser(userId, signUpParameters.PhoneNumber, 
            signUpParameters.FirstName, signUpParameters.LastName);
        await _search.IndexUser(new Data.Search.UserSearchModel(userId, "", 
            signUpParameters.FirstName, signUpParameters.LastName, signUpParameters.PhoneNumber));
        if (!await _authorizationCompletion.CreateOrUpdatePendingAsync(
                authKeyId, userId, signUpParameters.PhoneNumber, apiLayer))
        {
            return (TLAuthorization)RpcErrorGenerator.GenerateError(500,
                "INTERNAL"u8);
        }
        TLAuthorization? completed = await _authorizationCompletion
            .CompleteAsync(authKeyId);
        return completed ?? (TLAuthorization)RpcErrorGenerator.GenerateError(
            500, "INTERNAL"u8);
    }
    
    protected static SignUpParameters GetSignUpParameters(TLBytes q)
    {
        var signUp = new SignUp(q.AsSpan());
        var phoneNumber = Encoding.UTF8.GetString(signUp.PhoneNumber);
        var phoneCodeHash = Encoding.UTF8.GetString(signUp.PhoneCodeHash);
        var firstName = Encoding.UTF8.GetString(signUp.FirstName);
        var lastName = Encoding.UTF8.GetString(signUp.LastName);
        return new SignUpParameters(phoneNumber, phoneCodeHash, firstName, lastName);
    }

    protected readonly record struct SignUpParameters(string PhoneNumber,
        string PhoneCodeHash, string FirstName, string LastName);

    private static TLLoginAttempt BuildLoginAttempt(long authKeyId,
        string phoneNumber, string phoneCodeHash, int apiLayer, int now,
        int expiresAt, long? userId)
    {
        var builder = LoginAttempt.Builder()
            .AuthKeyId(authKeyId)
            .Phone(Encoding.UTF8.GetBytes(phoneNumber))
            .PhoneCodeHash(Encoding.UTF8.GetBytes(phoneCodeHash))
            .ApiLayer(apiLayer)
            .VerifiedAt(now)
            .CreatedAt(now)
            .ExpiresAt(expiresAt);
        if (userId is { } id)
        {
            builder = builder.UserId(id);
        }
        return builder.Build();
    }

    private int UnixNow() =>
        checked((int)_timeProvider.GetUtcNow().ToUnixTimeSeconds());

    protected TLUser SaveUser(long userId ,string phoneNumber, string firstName, string lastName)
    {
        using var photo = UserProfilePhotoEmpty.Builder().Build();
        var user = User.Builder()
            .Id(userId)
            .Phone(Encoding.UTF8.GetBytes(phoneNumber))
            .FirstName(Encoding.UTF8.GetBytes(firstName))
            .LastName(Encoding.UTF8.GetBytes(lastName))
            .AccessHash(_random.NextLong())
            .Photo(photo.TLBytes!.Value.AsSpan())
            .Build();
        _userRepository.PutUser(user);
        return user;
    }

    public async ValueTask<bool> SaveAppInfo(TLAppInfo info)
    {
        _appInfoRepository.PutAppInfo(info);
        return await _unitOfWork.SaveAsync();
    }

}
