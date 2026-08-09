// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Crypto;

namespace Ferrite.Services.Gateway;

public class VerificationGateway : IVerificationGateway
{
    private readonly IRandomGenerator _random;

    public VerificationGateway(IRandomGenerator random)
    {
        _random = random;
    }
    
    public ValueTask<string> SendNotification(string phone)
    {
        return ValueTask.FromResult(PrintCode(GetCode().ToString()));
    }

    public ValueTask<string> SendCodeViaCall(string phone)
    {
        return ValueTask.FromResult(PrintCode(GetCode().ToString()));
    }

    public ValueTask<string> SendCodeViaFlashCall(string phone)
    {
        return ValueTask.FromResult(PrintCode(GetCode().ToString()));
    }

    public ValueTask<string> SendCodeViaMissedCall(string phone)
    {
        return ValueTask.FromResult(PrintCode(GetCode().ToString()));
    }

    public ValueTask<string> SendEmail(string phone)
    {
        return ValueTask.FromResult(PrintCode(GetCode().ToString()));
    }

    public ValueTask<string> SendSms(string phone)
    {
        return ValueTask.FromResult(PrintCode(GetCode().ToString()));
    }

    public ValueTask<SentCodeType> Resend(string phone, string code)
    {
        PrintCode(code);
        return ValueTask.FromResult(SentCodeType.Sms);
    }

    private string PrintCode(string code)
    {
        string codeStr = code;
        Console.WriteLine($"Verification code is ==> {codeStr}");
        return codeStr;
    }

    private int GetCode()
    {
#if DEBUG
        var code = 12345;
#else
        var code = _random.GetNext(10000, 99999);
#endif
        return code;
    }
}