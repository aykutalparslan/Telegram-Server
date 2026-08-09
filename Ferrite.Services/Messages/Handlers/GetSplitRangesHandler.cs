// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer;

namespace Ferrite.Services.Handlers.MessageMethods;

public sealed class GetSplitRangesHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IUnitOfWork _unitOfWork;

    public GetSplitRangesHandler(IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository)
    {
        _authorizationRepository = authorizationRepository;

        _unitOfWork = unitOfWork;
    }

    [TLFunction(Constructors.baseLayer_GetSplitRanges)]
    public async Task<TLBytes> Handle(long authKeyId, TLBytes q)
    {
        using var auth = await _authorizationRepository
            .GetAuthorizationAsync(authKeyId);
        if (auth == null)
        {
            return RpcErrorGenerator.GenerateError(400, "AUTH_KEY_INVALID"u8);
        }

        var ranges = new Vector();
        using (TLMessageRange range = MessageRange.Builder()
                   .MinId(1)
                   .MaxId(int.MaxValue)
                   .Build())
        {
            ranges.AppendTLObject(range.AsSpan());
        }
        byte[] bytes = ranges.ToReadOnlySpan().ToArray();
        return new TLBytes(bytes, 0, bytes.Length);
    }
}
