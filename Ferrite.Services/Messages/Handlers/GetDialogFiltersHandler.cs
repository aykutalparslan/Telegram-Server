// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;
using Ferrite.TL.baseLayer.dto;
using Ferrite.TL.baseLayer.messages;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class GetDialogFiltersHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly DialogFilterStore _filters;

    public GetDialogFiltersHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository,
        DialogFilterStore filters)
    {
        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
        _filters = filters;
    }

    [TLFunction(Constructors.baseLayer_GetDialogFilters)]
    public async Task<TLDialogFilters> Handle(long authKeyId, TLBytes q)
    {
        long userId;
        using (TLAuthInfo? auth = await _authorizationRepository
                   .GetAuthorizationAsync(authKeyId))
        {
            if (auth == null || !auth.Value.AsAuthInfo().LoggedIn)
            {
                return (TLDialogFilters)RpcErrorGenerator.GenerateError(401,
                    "AUTH_KEY_INVALID"u8);
            }
            userId = auth.Value.AsAuthInfo().UserId;
        }

        List<DialogFilterSnapshot> stored = await _filters.GetFiltersAsync(userId);
        bool tagsEnabled = await _filters.GetTagsEnabledAsync(userId);
        var values = new Vector();
        foreach (DialogFilterSnapshot filter in stored)
        {
            values.AppendTLObject(filter.Filter);
        }
        var builder = DialogFilters.Builder().Filters(values);
        if (tagsEnabled) builder = builder.TagsEnabled(true);
        return builder.Build();
    }
}
