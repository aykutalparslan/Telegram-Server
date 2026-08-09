// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System;
namespace Ferrite.Core.Exceptions;

public class MTProtoSecurityException: Exception
{
    public MTProtoSecurityException()
    {
    }

    public MTProtoSecurityException(string message)
        : base(message)
    {
    }

    public MTProtoSecurityException(string message, Exception inner)
        : base(message, inner)
    {
    }
}

