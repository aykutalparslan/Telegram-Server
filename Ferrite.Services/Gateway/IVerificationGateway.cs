// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services.Gateway;

public interface IVerificationGateway
{
    public ValueTask<string> SendCodeViaCall(string phone);
    public ValueTask<string> SendCodeViaFlashCall(string phone);
    public ValueTask<string> SendCodeViaMissedCall(string phone);
    public ValueTask<string> SendEmail(string phone);
    public ValueTask<string> SendNotification(string phone);
    public ValueTask<string> SendSms(string phone);
    public ValueTask<SentCodeType> Resend(string phone, string code);
}