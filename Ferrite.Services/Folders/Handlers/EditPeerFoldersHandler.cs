// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.folders;

namespace Ferrite.Services.Handlers.FolderMethods;

public sealed class EditPeerFoldersHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly DialogOrganizationStore _organization;

    public EditPeerFoldersHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository,
        DialogOrganizationStore organization)
    {
        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
        _organization = organization;
    }

    [TLFunction(Constructors.baseLayer_EditPeerFolders)]
    public async Task<TLUpdates> Handle(long authKeyId, TLBytes q)
    {
        using TLAuthInfo? auth = await _authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        if (auth == null || !auth.Value.AsAuthInfo().LoggedIn)
        {
            return (TLUpdates)RpcErrorGenerator.GenerateError(401,
                "AUTH_KEY_INVALID"u8);
        }
        long userId = auth.Value.AsAuthInfo().UserId;

        var request = (EditPeerFolders)q;
        Vector folderPeers = request.FolderPeers;
        var moves = new List<DialogFolderMove>(folderPeers.Count);
        for (int i = 0; i < folderPeers.Count; i++)
        {
            var item = (InputFolderPeer)folderPeers.ReadTLObject();
            if (!PeerResolver.TryResolveInputPeerDialogKey(item.Get_PeerView(),
                    userId, out DialogPeerKey peer))
            {
                return (TLUpdates)RpcErrorGenerator.GenerateError(400,
                    "PEER_ID_INVALID"u8);
            }
            moves.Add(new DialogFolderMove(peer, item.FolderId));
        }

        return await _organization.EditPeerFoldersAsync(authKeyId, userId, moves);
    }
}
