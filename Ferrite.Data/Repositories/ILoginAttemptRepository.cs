// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public interface ILoginAttemptRepository
{
    ValueTask PutAttemptAsync(TLDto.TLLoginAttempt attempt, TimeSpan ttl,
        CancellationToken cancellationToken = default);
    ValueTask<TLDto.TLLoginAttempt?> GetByAuthKeyAsync(long authKeyId,
        CancellationToken cancellationToken = default);
    ValueTask<TLDto.TLLoginAttempt?> GetByPhoneHashAsync(string phone,
        string phoneCodeHash, CancellationToken cancellationToken = default);
    ValueTask<TLDto.TLLoginAttempt?> ConsumeByAuthKeyAsync(long authKeyId,
        CancellationToken cancellationToken = default);
    ValueTask<TLDto.TLLoginAttempt?> ConsumeByPhoneHashAsync(string phone,
        string phoneCodeHash, CancellationToken cancellationToken = default);
}
