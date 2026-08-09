// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Core.RequestChain;

public interface ILinkedHandler : ITLHandler
{
    public ILinkedHandler? Next { get; set; }
    public ILinkedHandler SetNext(ILinkedHandler value);
}