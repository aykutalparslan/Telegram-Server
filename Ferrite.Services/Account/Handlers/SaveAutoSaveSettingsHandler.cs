// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.account;
using TLSavePreset = Ferrite.TL.baseLayer.TLAutoSaveSettings;
using SavePreset = Ferrite.TL.baseLayer.AutoSaveSettings;

namespace Ferrite.Services.Handlers.AccountMethods;

public sealed class SaveAutoSaveSettingsHandler : AccountSettingsHandlerBase
{
    public SaveAutoSaveSettingsHandler(AccountSettingsStore store) : base(store) { }

    [TLFunction(Constructors.baseLayer_SaveAutoSaveSettings)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long? userId = await GetUserIdAsync(authKeyId);
        if (!userId.HasValue) return AuthError();

        var request = new SaveAutoSaveSettings(q.AsSpan());
        int selected = (request.Users ? 1 : 0) + (request.Chats ? 1 : 0) +
                       (request.Broadcasts ? 1 : 0) + (request.Flags[3] ? 1 : 0);
        if (selected != 1) return Invalid("SETTINGS_INVALID");

        int scope;
        DialogPeerKey peer = new(TLPeer.PeerType.PeerUser, 0);
        if (request.Flags[3])
        {
            if (!PeerResolver.TryResolveInputPeerDialogKey(request.Get_PeerView(),
                    userId.Value, out peer))
            {
                return Invalid("PEER_ID_INVALID");
            }
            scope = AccountSettingsStore.AutoSavePeer;
        }
        else if (request.Users) scope = AccountSettingsStore.AutoSaveUsers;
        else if (request.Chats) scope = AccountSettingsStore.AutoSaveChats;
        else scope = AccountSettingsStore.AutoSaveBroadcasts;

        using TLSavePreset settings = ((SavePreset)request.Settings)
            .Clone().Build();
        return await Store.SaveAutoSaveAsync(userId.Value, scope, peer, settings);
    }
}
