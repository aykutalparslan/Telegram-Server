// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using TLDto = Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public interface ILoginTokenRepository
{
    ValueTask PutQrTokenAsync(TLDto.TLQrLoginToken token, TimeSpan ttl,
        CancellationToken cancellationToken = default);
    ValueTask<TLDto.TLQrLoginToken?> GetQrTokenAsync(ReadOnlyMemory<byte> token,
        CancellationToken cancellationToken = default);
    ValueTask<bool> TryReplaceQrTokenAsync(ReadOnlyMemory<byte> token, int expectedState,
        TLDto.TLQrLoginToken replacement, TimeSpan ttl,
        CancellationToken cancellationToken = default);
    ValueTask<TLDto.TLQrLoginToken?> ConsumeQrTokenAsync(ReadOnlyMemory<byte> token,
        int expectedState, CancellationToken cancellationToken = default);

    ValueTask PutWebTokenAsync(TLDto.TLWebAuthorizationToken token, TimeSpan ttl,
        CancellationToken cancellationToken = default);
    ValueTask<TLDto.TLWebAuthorizationToken?> GetWebTokenAsync(
        ReadOnlyMemory<byte> tokenDigest, CancellationToken cancellationToken = default);
    ValueTask<TLDto.TLWebAuthorizationToken?> ConsumeWebTokenAsync(
        ReadOnlyMemory<byte> tokenDigest, CancellationToken cancellationToken = default);
}
