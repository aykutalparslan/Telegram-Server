// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Collections.Frozen;
using Ferrite.TL;

namespace Ferrite.Core.Execution;

/// <summary>
/// Declarative auth-gate policy for <see cref="ExecutionEngine"/>. Constructors in
/// <see cref="UnauthorizedMethods"/> may run without an authorized key (login,
/// help/langpack bootstrap, the MTProto handshake and service messages, the
/// invocation wrappers); constructors in <see cref="TempKeyAllowedMethods"/> may
/// run on a temp-unbound (PFS) auth key.
///
/// Some entries are older method-id variants still sent by pinned current clients,
/// so the integer values are preserved verbatim rather than replaced with the
/// layer-214 <c>Constructors.*</c> constants. Replacing the magic ints with
/// verified constants is deferred (it must not change any value).
/// </summary>
internal static class AuthPolicy
{
    public static readonly FrozenSet<int> UnauthorizedMethods = new[]
    {
        -1502141361,                    // auth.sendCode
        1056025023,                     // auth.resendCode
        unchecked((int)0x1f040578),     // auth.cancelCode
        1418342645,                     // account.getPassword
        -779399914,                     // auth.checkPassword
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
        -2131827673,                    // auth.signUp
        Constructors.baseLayer_SignUp,  // auth.signUp (layer 214)
        -1126886015,                    // auth.signIn
        -1923962543,                    // auth.signIn
        -1518699091,                    // auth.importAuthorization
        -990308245,                     // help.getConfig
        531836966,                      // help.getNearestDC
        1378703997,                     // help.getAppUpdate
        1375900482,                     // help.getCDNConfig
        1935116200,                     // help.getCountriesList
        -219008246,                     // langpack.getLangPack
        -269862909,                     // langpack.getStrings
        -845657435,                     // langpack.getDifference
        1120311183,                     // langpack.getLanguages
        1784243458,                     // langpack.getLanguage
        -1043505495,                    // InitConnection
        -841733627,                     // auth.bindTempAuthKey
        -1188971260,                    // get_future_salts
        unchecked((int)0xbe7e8ef1),     // req_pq_multi
        unchecked((int)0xd712e4be),     // req_dh_params
        unchecked((int)0xf5045f1f),     // set_client_dh_params
        unchecked((int)0x7abe77ec),     // ping
        unchecked((int)0xf3427b8c),     // ping_delay_disconnect
        -414113498,                     // destroy_session
        unchecked((int)0xd1435160),     // destroy_auth_key
        1491380032,                     // rpc_drop_answer
        1658238041,                     // msgs_ack
        2018609336,                     // initConnection
        unchecked((int)0xda9b0d0d),     // invokeWithLayer
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
        -990308245,                     // help.getConfig
        531836966,                      // help.getNearestDC
        -841733627,                     // auth.bindTempAuthKey
        Constructors.baseLayer_InvokeAfterMsg,
        Constructors.baseLayer_InvokeAfterMsgs,
        Constructors.baseLayer_InvokeWithoutUpdates,
        Constructors.baseLayer_InvokeWithMessagesRange,
        Constructors.baseLayer_InvokeWithGooglePlayIntegrityPrefix,
        Constructors.baseLayer_InvokeWithApnsSecretPrefix,
        Constructors.baseLayer_InvokeWithReCaptchaPrefix,
    }.ToFrozenSet();
}
