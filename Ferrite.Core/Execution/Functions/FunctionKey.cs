// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Core.Execution.Functions;

public readonly record struct FunctionKey
{
    private readonly int _layer;
    private readonly int _constructorNumber;
    
    public FunctionKey(int layer, int constructorNumber)
    {
        _layer = layer;
        _constructorNumber = constructorNumber;
    }

    public int Constructor => _constructorNumber;
}
    