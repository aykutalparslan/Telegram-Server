// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public interface IClientLayerRepository
{
    bool PutClientLayer(TLClientLayer layer);
    TLClientLayer? GetClientLayer(long authKeyId);
    ValueTask<TLClientLayer?> GetClientLayerAsync(long authKeyId);
    IReadOnlyList<TLClientLayer> GetClientLayers();
    bool DeleteClientLayer(long authKeyId);
}
