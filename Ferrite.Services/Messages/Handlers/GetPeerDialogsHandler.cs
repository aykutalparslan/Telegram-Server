// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class GetPeerDialogsHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly DialogBuilder _dialogs;

    public GetPeerDialogsHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, DialogBuilder dialogs)
    {
        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
        _dialogs = dialogs;
    }

    [TLFunction(Constructors.baseLayer_GetPeerDialogs)]
    public async Task<TLPeerDialogs> Handle(long authKeyId, TLBytes q)
    {
        long userId;
        using (TLAuthInfo? auth = await _authorizationRepository
                   .GetAuthorizationAsync(authKeyId))
        {
            if (auth == null || !auth.Value.AsAuthInfo().LoggedIn)
            {
                return (TLPeerDialogs)RpcErrorGenerator.GenerateError(401,
                    "AUTH_KEY_INVALID"u8);
            }
            userId = auth.Value.AsAuthInfo().UserId;
        }
        var requested = new List<DialogPeerKey>();
        var seenRequested = new HashSet<DialogPeerKey>();
        var peersVector = ((GetPeerDialogs)q).Peers;
        int peerCount = peersVector.Count;
        for (int i = 0; i < peerCount; i++)
        {
            InputDialogPeerView dialogPeerView = peersVector.ReadTLObject();
            if (!dialogPeerView.Is(out InputDialogPeer dialogPeer))
            {
                continue;
            }
            if (!PeerResolver.TryResolveInputPeerDialogKey(dialogPeer.Get_PeerView(),
                    userId, out var key))
            {
                continue;
            }
            if (seenRequested.Add(key))
            {
                requested.Add(key);
            }
        }
        return await _dialogs.GetPeerDialogsAsync(authKeyId, userId, requested);
    }
}
