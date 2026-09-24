// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;

namespace Ferrite.Services.Auth;

public static class ImportedAuthorizations
{
    public static async ValueTask<long> GetHomeAuthKeyIdAsync(
        this IAuthorizationRepository repository, long authKeyId) =>
        await repository.GetImportSourceAsync(authKeyId) ?? authKeyId;

    public static async ValueTask<List<long>> GetAuthKeyIdsImportedFromAsync(
        this IAuthorizationRepository repository, string phone, long sourceAuthKeyId)
    {
        var imported = new List<long>();
        foreach (var authorization in await repository.GetAuthorizationsAsync(phone))
        {
            long authKeyId = authorization.AsAuthInfo().AuthKeyId;
            if (authKeyId != sourceAuthKeyId &&
                await repository.GetImportSourceAsync(authKeyId) == sourceAuthKeyId)
            {
                imported.Add(authKeyId);
            }
        }

        return imported;
    }
}
