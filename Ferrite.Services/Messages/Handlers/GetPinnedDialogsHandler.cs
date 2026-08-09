// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class GetPinnedDialogsHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IDialogOrganizationRepository _dialogOrganizationRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly DialogBuilder _dialogs;

    public GetPinnedDialogsHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IDialogOrganizationRepository dialogOrganizationRepository, DialogBuilder dialogs)
    {
        _authorizationRepository = authorizationRepository;
        _dialogOrganizationRepository = dialogOrganizationRepository;

        _unitOfWork = unitOfWork;
        _dialogs = dialogs;
    }

    [TLFunction(Constructors.baseLayer_GetPinnedDialogs)]
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

        int folderId = ((GetPinnedDialogs)q).FolderId;
        if (folderId is not (0 or 1))
        {
            return (TLPeerDialogs)RpcErrorGenerator.GenerateError(400,
                "FOLDER_ID_INVALID"u8);
        }
        Dictionary<DialogPeerKey, DialogOrganizationState> states =
            await DialogOrganizationStore.ReadPeerStatesAsync(
                _dialogOrganizationRepository, userId);
        DialogPeerKey[] pinned = states.Where(x => x.Value.FolderId == folderId &&
                x.Value.Pinned)
            .OrderByDescending(x => x.Value.PinOrder)
            .ThenByDescending(x => (int)x.Key.Type)
            .ThenByDescending(x => x.Key.Id)
            .Select(x => x.Key)
            .ToArray();
        return await _dialogs.GetPeerDialogsAsync(authKeyId, userId, pinned);
    }
}
