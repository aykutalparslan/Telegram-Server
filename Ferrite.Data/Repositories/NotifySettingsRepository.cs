// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Data.Repositories;

public class NotifySettingsRepository : INotifySettingsRepository
{
    private readonly IKVStore _store;
    public NotifySettingsRepository(IKVStore store)
    {
        _store = store;
        _store.SetSchema(new TableDefinition("ferrite", "notify_settings",
            new KeyDefinition("pk",
                new DataColumn { Name = "auth_key_id", Type = DataType.Long },
                new DataColumn { Name = "notify_peer_type", Type = DataType.Int }, 
                new DataColumn { Name = "peer_type", Type = DataType.Int },
                new DataColumn { Name = "peer_id", Type = DataType.Long },
                new DataColumn { Name = "device_type", Type = DataType.Int })));
    }
    public bool PutNotifySettings(long authKeyId, int notifyPeerType, int peerType, long peerId, int deviceType, TLPeerNotifySettings settings)
    {
        using TLNotifySettingsState state = NotifySettingsState.Builder()
            .AuthKeyId(authKeyId).NotifyPeerType(notifyPeerType)
            .PeerType(peerType).PeerId(peerId).DeviceType(deviceType)
            .Settings(settings.AsSpan()).Build();
        return _store.Put(state.AsSpan().ToArray(), authKeyId, notifyPeerType,
            peerType, peerId, deviceType);
    }

    public IReadOnlyCollection<TLPeerNotifySettings> GetNotifySettings(long authKeyId, int notifyPeerType, int peerType, long peerId, int deviceType)
    {
        List<TLPeerNotifySettings> results = new();
       
        var iter = _store.Iterate(authKeyId,
            notifyPeerType, peerType, peerId, deviceType);
        foreach (var settingBytes in iter)
        {
            using var state = new TLNotifySettingsState(settingBytes, 0,
                settingBytes.Length);
            results.Add(state.AsNotifySettingsState().Get_Settings());
        }

        return results;
    }

    public IReadOnlyCollection<TLNotifySettingsState> GetNotifyExceptions(
        long authKeyId)
    {
        List<TLNotifySettingsState> results = new();
        foreach (byte[] bytes in _store.Iterate(authKeyId))
        {
            results.Add(new TLNotifySettingsState(bytes, 0, bytes.Length));
        }
        return results;
    }

    public bool DeleteNotifySettings(long authKeyId)
    {
        return _store.Delete(authKeyId);
    }
}
