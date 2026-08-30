// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Collections.Frozen;
using Ferrite.TL;

namespace Ferrite.Core.Execution;

internal static class AuthPolicy
{
    public static readonly FrozenSet<int> UnauthorizedMethods = new[]
    {
        -1502141361,
        1056025023,
        unchecked((int)0x1f040578),
        1418342645,
        -779399914,
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
        -2131827673,
        Constructors.baseLayer_SignUp,
        -1126886015,
        -1923962543,
        -1518699091,
        -990308245,
        Constructors.baseLayer_GetAppConfig,
        531836966,
        1378703997,
        1375900482,
        1935116200,
        -219008246,
        -269862909,
        -845657435,
        1120311183,
        unchecked((int)0x800fd57d),
        1784243458,
        -1043505495,
        -841733627,
        -1188971260,
        unchecked((int)0xbe7e8ef1),
        unchecked((int)0xd712e4be),
        unchecked((int)0xf5045f1f),
        unchecked((int)0x7abe77ec),
        unchecked((int)0xf3427b8c),
        -414113498,
        unchecked((int)0xd1435160),
        1491380032,
        1658238041,
        2018609336,
        unchecked((int)0xda9b0d0d),
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
        -990308245,
        Constructors.baseLayer_GetAppConfig,
        531836966,
        -841733627,
        unchecked((int)0xda9b0d0d),
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
