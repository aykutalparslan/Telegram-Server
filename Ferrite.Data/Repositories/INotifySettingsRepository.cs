// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public interface INotifySettingsRepository
{
    public bool PutNotifySettings(long authKeyId, int notifyPeerType, int peerType, long peerId, int deviceType, TLPeerNotifySettings settings);
    public IReadOnlyCollection<TLPeerNotifySettings> GetNotifySettings(long authKeyId, int notifyPeerType, int peerType, long peerId, int deviceType);
    public IReadOnlyCollection<TLNotifySettingsState> GetNotifyExceptions(long authKeyId);
    public bool DeleteNotifySettings(long authKeyId);
}
