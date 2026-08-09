// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Collections.Immutable;

namespace Ferrite.Data.Repositories;

public class TableDefinition
{
    public readonly string Keyspace;
    public readonly string Name;
    public readonly KeyDefinition PrimaryKey;
    public readonly ImmutableList<KeyDefinition> SecondaryIndices;
    public string FullName { get; }

    public TableDefinition(string keyspace, string name, KeyDefinition primaryKey, params KeyDefinition[] secondaryIndices)
    {
        Keyspace = keyspace;
        Name = name;
        FullName = keyspace + "." + name;
        PrimaryKey = primaryKey;
        SecondaryIndices = ImmutableList.Create(secondaryIndices);
        foreach (var index in SecondaryIndices)
        {
            index.Parent = this;
        }
    }
}