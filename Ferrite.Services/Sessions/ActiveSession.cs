// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System;
using System.Diagnostics.CodeAnalysis;

namespace Ferrite.Services.Sessions;

public class ActiveSession
{
    private readonly WeakReference<IMTProtoConnection> _ref;
    public ActiveSession(IMTProtoConnection connection)
    {
        _ref = new(connection);
    }

    public bool TryGetConnection([NotNullWhen(true)] out IMTProtoConnection? connection)
    {
        return _ref.TryGetTarget(out connection);
    }
}
