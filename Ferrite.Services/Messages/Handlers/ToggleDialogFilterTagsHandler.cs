// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class ToggleDialogFilterTagsHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly DialogFilterStore _filters;

    public ToggleDialogFilterTagsHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository,
        DialogFilterStore filters)
    {
        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
        _filters = filters;
    }

    [TLFunction(Constructors.baseLayer_ToggleDialogFilterTags)]
    public async Task<TLBool> Handle(long authKeyId, TLBytes q)
    {
        long userId;
        using (TLAuthInfo? auth = await _authorizationRepository
                   .GetAuthorizationAsync(authKeyId))
        {
            if (auth == null || !auth.Value.AsAuthInfo().LoggedIn)
            {
                return (TLBool)RpcErrorGenerator.GenerateError(401,
                    "AUTH_KEY_INVALID"u8);
            }
            userId = auth.Value.AsAuthInfo().UserId;
        }
        bool enabled = ((ToggleDialogFilterTags)q).Enabled;
        return await _filters.ToggleTagsAsync(authKeyId, userId, enabled);
    }
}
