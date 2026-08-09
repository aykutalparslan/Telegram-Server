// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

namespace Ferrite.Services;

/// <summary>
/// Selects which permanent authorizations receive an update for one user.
/// </summary>
public sealed class UpdateDeliveryScope
{
    private readonly HashSet<long>? _targetAuthKeyIds;
    private readonly HashSet<long> _excludedAuthKeyIds;

    private UpdateDeliveryScope(IEnumerable<long>? targetAuthKeyIds,
        IEnumerable<long>? excludedAuthKeyIds)
    {
        _targetAuthKeyIds = targetAuthKeyIds == null
            ? null
            : new HashSet<long>(targetAuthKeyIds);
        _excludedAuthKeyIds = excludedAuthKeyIds == null
            ? new HashSet<long>()
            : new HashSet<long>(excludedAuthKeyIds);
    }

    public static UpdateDeliveryScope All { get; } = new(null, null);

    public static UpdateDeliveryScope ForAuthKey(long authKeyId) =>
        new(new[] { authKeyId }, null);

    public static UpdateDeliveryScope ForAuthKeys(IEnumerable<long> authKeyIds,
        IEnumerable<long>? excludedAuthKeyIds = null)
    {
        ArgumentNullException.ThrowIfNull(authKeyIds);
        return new UpdateDeliveryScope(authKeyIds, excludedAuthKeyIds);
    }

    public static UpdateDeliveryScope ExcludingAuthKeys(IEnumerable<long> authKeyIds)
    {
        ArgumentNullException.ThrowIfNull(authKeyIds);
        return new UpdateDeliveryScope(null, authKeyIds);
    }

    public bool AreTargetsOwnedBy(IReadOnlySet<long> ownedAuthKeyIds) =>
        _targetAuthKeyIds == null || _targetAuthKeyIds.IsSubsetOf(ownedAuthKeyIds);

    public bool Includes(long authKeyId) =>
        (_targetAuthKeyIds == null || _targetAuthKeyIds.Contains(authKeyId)) &&
        !_excludedAuthKeyIds.Contains(authKeyId);
}
