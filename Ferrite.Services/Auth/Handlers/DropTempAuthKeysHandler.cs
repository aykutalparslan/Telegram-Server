// SPDX-License-Identifier: AGPL-3.0-or-later
// Copyright (C) 2022-2026 Aykut Alparslan KOC

using Ferrite.Data.Repositories;
using Ferrite.TL;

namespace Ferrite.Services.Handlers.AuthMethods;

public sealed class DropTempAuthKeysHandler
{
    private readonly IBoundAuthKeyRepository _boundAuthKeyRepository;
    private readonly ITempAuthKeyRepository _tempAuthKeyRepository;

    private readonly IUnitOfWork _unitOfWork;

    public DropTempAuthKeysHandler(IUnitOfWork unitOfWork, IBoundAuthKeyRepository boundAuthKeyRepository, ITempAuthKeyRepository tempAuthKeyRepository)
    {
        _boundAuthKeyRepository = boundAuthKeyRepository;
        _tempAuthKeyRepository = tempAuthKeyRepository;

        _unitOfWork = unitOfWork;
    }

    [TLFunction(Constructors.baseLayer_DropTempAuthKeys)]
    public async ValueTask<TLBool> Handle(long authKeyId, TLBytes q)
    {
        foreach (long key in _boundAuthKeyRepository.GetTempAuthKeys(authKeyId))
        {
            // Preserve the existing Core handler behavior: except_auth_keys was ignored.
            _tempAuthKeyRepository.DeleteTempAuthKey(key);
        }

        bool result = await _unitOfWork.SaveAsync();
        return result ? new BoolTrue() : new BoolFalse();
    }
}
