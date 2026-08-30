// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;

namespace Ferrite.Services.Calls;

public static class PhoneCallReasons
{
    public static TLPhoneCallDiscardReason Build(int reasonConstructor)
    {
        switch (reasonConstructor)
        {
            case Constructors.baseLayer_PhoneCallDiscardReasonMissed:
                return PhoneCallDiscardReasonMissed.Builder().Build();
            case Constructors.baseLayer_PhoneCallDiscardReasonDisconnect:
                return PhoneCallDiscardReasonDisconnect.Builder().Build();
            case Constructors.baseLayer_PhoneCallDiscardReasonBusy:
                return PhoneCallDiscardReasonBusy.Builder().Build();
            default:
                return PhoneCallDiscardReasonHangup.Builder().Build();
        }
    }
}
