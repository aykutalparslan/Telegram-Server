// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class GetDialogUnreadMarksHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;
    private readonly IDialogOrganizationRepository _dialogOrganizationRepository;

    private readonly IUnitOfWork _unitOfWork;

    public GetDialogUnreadMarksHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository, IDialogOrganizationRepository dialogOrganizationRepository)
    {
        _authorizationRepository = authorizationRepository;
        _dialogOrganizationRepository = dialogOrganizationRepository;

        _unitOfWork = unitOfWork;
    }

    [TLFunction(Constructors.baseLayer_GetDialogUnreadMarks)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        long userId;
        using (TLAuthInfo? auth = await _authorizationRepository
                   .GetAuthorizationAsync(authKeyId))
        {
            if (auth == null || !auth.Value.AsAuthInfo().LoggedIn)
            {
                return RpcErrorGenerator.GenerateError(401, "AUTH_KEY_INVALID"u8);
            }
            userId = auth.Value.AsAuthInfo().UserId;
        }
        if (((GetDialogUnreadMarks)q).Flags[0])
        {
            return RpcErrorGenerator.GenerateError(400,
                "SAVED_PEER_ID_INVALID"u8);
        }

        Dictionary<DialogPeerKey, DialogOrganizationState> states =
            await DialogOrganizationStore.ReadPeerStatesAsync(
                _dialogOrganizationRepository, userId);
        var marks = new Vector();
        foreach (DialogPeerKey peer in states.Where(x => x.Value.UnreadMark)
                     .OrderBy(x => x.Value.FolderId)
                     .ThenByDescending(x => x.Value.PinOrder)
                     .ThenBy(x => (int)x.Key.Type)
                     .ThenBy(x => x.Key.Id)
                     .Select(x => x.Key))
        {
            using TLDialogPeer dialogPeer = DialogOrganizationStore
                .BuildDialogPeer(peer);
            marks.AppendTLObject(dialogPeer.AsSpan());
        }
        byte[] result = marks.ToReadOnlySpan().ToArray();
        return new TLBytes(result, 0, result.Length);
    }
}
