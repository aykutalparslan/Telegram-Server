// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Collections.Frozen;
using Ferrite.TL;

namespace Ferrite.Core.Execution;

internal static class AuthPolicy
{
    public static readonly FrozenSet<int> UnauthorizedMethods = new[]
    {
        Constructors.baseLayer_SendCode,
        LegacyConstructors.ResendCode,
        Constructors.baseLayer_CancelCode,
        Constructors.baseLayer_GetPassword,
        Constructors.baseLayer_CheckPassword,
        Constructors.baseLayer_RequestPasswordRecovery,
        Constructors.baseLayer_RecoverPassword,
        Constructors.baseLayer_ExportLoginToken,
        Constructors.baseLayer_ImportLoginToken,
        Constructors.baseLayer_CheckRecoveryPassword,
        Constructors.baseLayer_ImportWebTokenAuthorization,
        Constructors.baseLayer_RequestFirebaseSms,
        Constructors.baseLayer_ResetLoginEmail,
        Constructors.baseLayer_ReportMissingCode,
        Constructors.baseLayer_SendVerifyEmailCode,
        Constructors.baseLayer_VerifyEmail,
        Constructors.layer104_AuthSignUp,
        Constructors.baseLayer_SignUp,
        LegacyConstructors.SignIn,
        Constructors.baseLayer_SignIn,
        Constructors.baseLayer_ImportAuthorization,
        Constructors.baseLayer_GetConfig,
        Constructors.baseLayer_GetAppConfig,
        Constructors.baseLayer_GetNearestDc,
        Constructors.baseLayer_GetAppUpdate,
        Constructors.baseLayer_GetCdnConfig,
        Constructors.baseLayer_GetCountriesList,
        Constructors.baseLayer_GetLangPack,
        Constructors.baseLayer_GetStrings,
        Constructors.baseLayer_LangpackGetDifference,
        Constructors.baseLayer_GetLanguages,
        Constructors.layer67_LangpackGetLanguages,
        Constructors.baseLayer_GetLanguage,
        Constructors.baseLayer_InitConnection,
        Constructors.baseLayer_BindTempAuthKey,
        Constructors.mtproto_GetFutureSalts,
        Constructors.mtproto_ReqPqMulti,
        Constructors.mtproto_ReqDhParams,
        Constructors.mtproto_SetClientDhParams,
        Constructors.mtproto_Ping,
        Constructors.mtproto_PingDelayDisconnect,
        Constructors.mtproto_DestroySession,
        Constructors.mtproto_DestroyAuthKey,
        Constructors.mtproto_RpcDropAnswer,
        Constructors.mtproto_MsgsAck,
        LegacyConstructors.InitConnection,
        Constructors.baseLayer_InvokeWithLayer,
        Constructors.baseLayer_InvokeAfterMsg,
        Constructors.baseLayer_InvokeAfterMsgs,
        Constructors.baseLayer_InvokeWithoutUpdates,
        Constructors.baseLayer_InvokeWithMessagesRange,
        Constructors.baseLayer_InvokeWithGooglePlayIntegrityPrefix,
        Constructors.baseLayer_InvokeWithApnsSecretPrefix,
        Constructors.baseLayer_InvokeWithReCaptchaPrefix,
    }.ToFrozenSet();

    public static readonly FrozenSet<int> TempKeyAllowedMethods = new[]
    {
        Constructors.baseLayer_GetConfig,
        Constructors.baseLayer_GetAppConfig,
        Constructors.baseLayer_GetNearestDc,
        Constructors.baseLayer_BindTempAuthKey,
        Constructors.baseLayer_InvokeWithLayer,
        Constructors.baseLayer_InitConnection,
        Constructors.baseLayer_InvokeAfterMsg,
        Constructors.baseLayer_InvokeAfterMsgs,
        Constructors.baseLayer_InvokeWithoutUpdates,
        Constructors.baseLayer_InvokeWithMessagesRange,
        Constructors.baseLayer_InvokeWithGooglePlayIntegrityPrefix,
        Constructors.baseLayer_InvokeWithApnsSecretPrefix,
        Constructors.baseLayer_InvokeWithReCaptchaPrefix,
    }.ToFrozenSet();
}
