// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class GetSuggestedDialogFiltersHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IUnitOfWork _unitOfWork;
    private readonly DialogFilterStore _filters;

    public GetSuggestedDialogFiltersHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository,
        DialogFilterStore filters)
    {
        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
        _filters = filters;
    }

    [TLFunction(Constructors.baseLayer_GetSuggestedDialogFilters)]
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

        List<byte[]> suggestions = await _filters.GetSuggestionsAsync(userId);
        var result = new Vector();
        foreach (byte[] suggestion in suggestions)
        {
            result.AppendTLObject(suggestion);
        }
        byte[] bytes = result.ToReadOnlySpan().ToArray();
        return new TLBytes(bytes, 0, bytes.Length);
    }
}
