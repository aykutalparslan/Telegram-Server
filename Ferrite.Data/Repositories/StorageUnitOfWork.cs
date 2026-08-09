// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Utils;

namespace Ferrite.Data.Repositories;

public sealed class StorageUnitOfWork : IUnitOfWork
{
    private readonly IWriteBatchAccessor _writeBatches;
    private readonly ILogger _log;

    public StorageUnitOfWork(IWriteBatchAccessor writeBatches, ILogger log)
    {
        _writeBatches = writeBatches;
        _log = log;
    }
    public bool Save()
    {
        try
        {
            _writeBatches.Flush();
            return true;
        }
        catch (Exception e)
        {
            _log.Error(e, "Failed to save storage changes");
            return false;
        }
    }

    public async ValueTask<bool> SaveAsync()
    {
        try
        {
            await _writeBatches.FlushAsync();
            return true;
        }
        catch (Exception e)
        {
            _log.Error(e, "Failed to save storage changes");
            return false;
        }
    }
}
