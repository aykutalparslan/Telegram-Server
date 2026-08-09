// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using System.Collections.Immutable;
using Nest;

namespace Ferrite.Data.Repositories;

public class KeyDefinition
{
    public readonly string Name;
    public readonly ImmutableList<DataColumn> Columns;
    private readonly ImmutableDictionary<string, int> _colsIndex;
    public string FullName { get; private set; }

    private TableDefinition _parent;
    public TableDefinition Parent
    {
        get => _parent;
        set
        {
            _parent = value;
            FullName = _parent.FullName + "_" + Name;
        }
    }

    public DataColumn this[int index] => Columns[index];
    public DataColumn this[string name] => Columns[_colsIndex[name]];
    public int GetOrdinal(string name) => _colsIndex[name];
    public bool HasColumn(string name) => _colsIndex.ContainsKey(name);

    public KeyDefinition(string name, params DataColumn[] args)
    {
        Name = name;
        Columns = ImmutableList.Create(args);
        var bld = ImmutableDictionary.CreateBuilder<string, int>();
        for (int i = 0; i < args.Length; i++)
        {
            bld.Add(args[i].Name, i);
        }

        _colsIndex = bld.ToImmutable();
    }
}