// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Crypto;
using Ferrite.Data.Repositories;
using Ferrite.TL;
using Ferrite.TL.baseLayer.auth;
using Ferrite.TL.baseLayer.dto;

namespace Ferrite.Services.Handlers.AuthMethods;

public sealed class ExportAuthorizationHandler
{
    private readonly IAuthorizationRepository _authorizationRepository;

    private readonly IRandomGenerator _random;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDataCenter _dataCenter;

    public ExportAuthorizationHandler(IRandomGenerator random, IUnitOfWork unitOfWork, IAuthorizationRepository authorizationRepository,
        IDataCenter dataCenter)
    {
        _authorizationRepository = authorizationRepository;

        _random = random;
        _unitOfWork = unitOfWork;
        _dataCenter = dataCenter;
    }

    [TLFunction(Constructors.baseLayer_ExportAuthorization)]
    public async ValueTask<TLExportedAuthorization> Handle(long authKeyId, TLBytes q)
    {
        var auth = await _authorizationRepository.GetAuthorizationAsync(authKeyId);
        if (auth == null)
        {
            return (TLExportedAuthorization)RpcErrorGenerator.GenerateError(400,
                "AUTH_KEY_UNREGISTERED"u8);
        }

        byte[] data = _random.GetRandomBytes(128);
        int dcId = new ExportAuthorization(q.AsSpan()).DcId;
        using TLExportedAuthInfo exported = ExportedAuthInfo.Builder()
            .Data(data)
            .UserId(auth.Value.AsAuthInfo().UserId)
            .Phone(auth.Value.AsAuthInfo().Phone)
            .AuthKeyId(auth.Value.AsAuthInfo().AuthKeyId)
            .NextDcId(dcId)
            .PreviousDcId(_dataCenter.Id)
            .Build();
        _authorizationRepository.PutExportedAuthorization(exported);
        await _unitOfWork.SaveAsync();
        return ExportedAuthorization.Builder()
            .Id(auth.Value.AsAuthInfo().UserId)
            .Bytes(data)
            .Build();
    }
}
