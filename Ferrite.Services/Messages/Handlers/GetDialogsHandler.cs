// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class GetDialogsHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly DialogBuilder _dialogs;

    public GetDialogsHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, DialogBuilder dialogs)
    {
        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
        _dialogs = dialogs;
    }

    [TLFunction(Constructors.baseLayer_GetDialogs)]
    public async Task<TLDialogs> Handle(long authKeyId, TLBytes q)
    {
        long userId;
        using (TLAuthInfo? auth = await _authorizationRepository
                   .GetAuthorizationAsync(authKeyId))
        {
            if (auth == null || !auth.Value.AsAuthInfo().LoggedIn)
            {
                return (TLDialogs)RpcErrorGenerator.GenerateError(401,
                    "AUTH_KEY_INVALID"u8);
            }
            userId = auth.Value.AsAuthInfo().UserId;
        }
        var request = (GetDialogs)q;
        DialogPeerKey? offsetPeer = PeerResolver.ResolveOptionalDialogPeer(
            request.Get_OffsetPeerView(), userId);
        int folderId = request.Flags[1] ? request.FolderId : 0;
        if (folderId is not (0 or 1))
        {
            return (TLDialogs)RpcErrorGenerator.GenerateError(400,
                "FOLDER_ID_INVALID"u8);
        }
        DialogQuery query = new DialogQuery(request.OffsetDate, request.OffsetId,
            Math.Max(0, request.Limit), offsetPeer, folderId);
        return await _dialogs.GetDialogsAsync(authKeyId, userId, query);
    }
}
